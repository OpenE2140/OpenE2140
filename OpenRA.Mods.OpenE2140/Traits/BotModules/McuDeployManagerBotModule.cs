#region Copyright & License Information

/*
 * Copyright (c) The OpenE2140 Developers and Contributors
 * This file is part of OpenE2140, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */

#endregion

using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Mods.OpenE2140.Traits.Mcu;
using OpenRA.Traits;
using TransformsInfo = OpenRA.Mods.OpenE2140.Traits.Mcu.TransformsInfo;

namespace OpenRA.Mods.OpenE2140.Traits.BotModules;

public enum BuildingType { Building, Defense, Mine, Refinery }

[TraitLocation(SystemActors.Player)]
[Desc("Manages AI MCUs.")]
public class McuDeployManagerBotModuleInfo : ConditionalTraitInfo
{
	[Desc("Actor types that are considered MCUs, which deploy into normal buildings.")]
	public readonly HashSet<string> McuTypes = [];

	[Desc("Actor types that are considered construction buildings (base builders).")]
	public readonly HashSet<string> ConstructionBuildingTypes = [];

	[Desc("Actor types that are considered MCUs, which deploy into refinery buildings.")]
	public readonly HashSet<string> RefineryTypes = [];

	[Desc("Actor types that are considered MCUs, which deploy into mine buildings.")]
	public readonly HashSet<string> MineTypes = [];

	[Desc("Actor types that are considered MCUs, which deploy into defense buildings.")]
	public readonly HashSet<string> DefenseMcuTypes = [];

	[Desc($"Interval in ticks after which defense center is reset back to null. Should be greater than {nameof(DefenseCenterUpdateInterval)}.")]
	public readonly int DefenseCenterResetInterval = 100;

	[Desc($"Interval in ticks after which defense center can be updated. Should be smaller than {nameof(DefenseCenterResetInterval)}.")]
	public readonly int DefenseCenterUpdateInterval = 50;

	[Desc("Distance cells, which allows updating defense center outside of reset/update interval.")]
	public readonly int DefenseCenterUpdateDistance = 4;

	[Desc("Minimum distance in cells from center of the base when checking for building placement.")]
	public readonly int MinBaseRadius = 2;

	[Desc("Radius in cells around the center of the base to expand.")]
	public readonly int MaxBaseRadius = 20;

	[Desc("Range at which to build defensive structures inside the inner defense circle (from center of the base).")]
	public readonly int InnerDefenseRadius = 5;

	[Desc("Range at which to build defensive structures inside the outer defense circle (from center of the base).")]
	public readonly int OuterDefenseRadius = 15;

	[Desc("Maximum range at which to build defensive structures (from center of the base).")]
	public readonly int MaximumDefenseRadius = 20;

	[Desc("Chance that the AI will place the defenses in the direction of the closest enemy building.")]
	public readonly int PlaceDefenseTowardsEnemyChance = 100;

	[Desc("Delay (in ticks) between looking for and giving out orders to new MCUs.")]
	public readonly int ScanForNewMcuInterval = 30;

	[Desc("Minimum distance in cells from center of the base when checking for MCU deployment location.")]
	public readonly int MinMoveRadius = 4;

	[Desc("If not possible to find a place to deploy, increase move radius by this amount until a suitable place is found.")]
	public readonly int MoveRadiusIncreaseOnFailed = 4;

	[Desc("Maximum distance in cells from center of the base when checking for MCU deployment location.",
		"When successful find a location, move radius will become MinMoveRadius.")]
	public readonly int MaxMoveRadius = 15;

	[Desc("Maximum number of retries before trying to find new deploy location for an MCU.")]
	public readonly int MaxRetryCount = 3;

	public override object Create(ActorInitializer init) { return new McuDeployManagerBotModule(init.Self, this); }

	public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
	{
		var nonExistingActors = this.ConstructionBuildingTypes.Where(a => !rules.Actors.ContainsKey(a)).ToList();
		if (nonExistingActors.Count > 0)
			throw new YamlException($"Unknown actors: {string.Join(", ", nonExistingActors)}");

		base.RulesetLoaded(rules, ai);
	}
}

public class McuDeployManagerBotModule : ConditionalTrait<McuDeployManagerBotModuleInfo>, IBotTick,
	IBotPositionsUpdated, IGameSaveTraitData, IBotRespondToAttack, IBotMcuDeployManager, INotifyTransformSequence
{
	private readonly OpenRA.World world;
	private readonly Player player;
	private readonly ActorIndex.OwnerAndNamesAndTrait<McuInfo> playerMcus;
	private readonly ActorIndex.OwnerAndNamesAndTrait<BuildingInfo> constructionBuildings;
	private readonly Dictionary<string, ICustomBuildingInfo> constructionBuildingInfos;
	private readonly Dictionary<Actor, McuDeployContext> mcuDeployContext = [];

	private IBotPositionsUpdated[] notifyPositionsUpdated = [];
	private List<IBotMcuDeployment> notifyMcuDeployment = [];
	private IBotEconomyManager? economyManager;
	private CPos? initialBaseCenter;
	private CPos? defenseCenter;
	private int? lastDefenseCenterUpdate;

	private int scanInterval;
	private bool firstTick = true;

	public McuDeployManagerBotModule(Actor self, McuDeployManagerBotModuleInfo info)
		: base(info)
	{
		this.world = self.World;
		this.player = self.Owner;

		this.playerMcus = new ActorIndex.OwnerAndNamesAndTrait<McuInfo>(this.world, this.Info.McuTypes, this.player);
		this.constructionBuildings = new ActorIndex.OwnerAndNamesAndTrait<BuildingInfo>(this.world, this.Info.ConstructionBuildingTypes, this.player);

		this.constructionBuildingInfos = this.Info.ConstructionBuildingTypes
			.Select(actorName => new
			{
				ActorName = actorName,
				BuildingInfo = CustomBuildingInfoWrapper.WrapIfNecessary(this.world.Map.Rules.Actors[actorName])
					?? throw new InvalidOperationException($"Unknown actor '{actorName}'")
			})
			.Where(a => a.BuildingInfo != null)
			.ToDictionary(a => a.ActorName, a => a.BuildingInfo!);
	}

	protected override void Created(Actor self)
	{
		this.notifyPositionsUpdated = self.Owner.PlayerActor.TraitsImplementing<IBotPositionsUpdated>().ToArray();
		this.notifyMcuDeployment = self.Owner.PlayerActor.TraitsImplementing<IBotMcuDeployment>().ToList();
		this.economyManager = self.Owner.PlayerActor.TraitOrDefault<IBotEconomyManager>();
	}

	protected override void TraitEnabled(Actor self)
	{
		// Avoid all AIs reevaluating assignments on the same tick, randomize their initial evaluation delay.
		this.scanInterval = this.world.LocalRandom.Next(this.Info.ScanForNewMcuInterval, this.Info.ScanForNewMcuInterval * 2);
	}

	void IBotPositionsUpdated.UpdatedBaseCenter(CPos newLocation)
	{
		this.initialBaseCenter = newLocation;
	}

	void IBotPositionsUpdated.UpdatedDefenseCenter(CPos newLocation)
	{
		if (this.lastDefenseCenterUpdate < this.Info.DefenseCenterUpdateInterval
			&& this.defenseCenter != null && (this.defenseCenter.Value - newLocation).Length < this.Info.DefenseCenterUpdateDistance)
			return;

		this.defenseCenter = newLocation;
		this.lastDefenseCenterUpdate = this.Info.DefenseCenterResetInterval;
	}

	int IBotMcuDeployManager.UndeployedMcuCount(IBot bot, string mcuType)
	{
		return this.playerMcus.Actors.Count(a => !a.IsDead && a.Info.Name == mcuType);
	}

	void IBotTick.BotTick(IBot bot)
	{
		if (this.firstTick)
		{
			this.initialBaseCenter = this.constructionBuildings.Alive().FirstOrDefault()?.Location;

			this.DeployMcus(bot, false);
			this.firstTick = false;
		}

		if (--this.scanInterval <= 0)
		{
			this.scanInterval = this.Info.ScanForNewMcuInterval;
			this.DeployMcus(bot, true);
		}

		if (this.lastDefenseCenterUpdate != null && --this.lastDefenseCenterUpdate <= 0)
		{
			this.defenseCenter = null;
			this.lastDefenseCenterUpdate = null;
		}
	}

	void INotifyTransformSequence.AfterTransform(Actor buildingActor)
	{
		var (mcuActor, context) = this.mcuDeployContext.FirstOrDefault(p => p.Value.BuildingActor == buildingActor);
		if (mcuActor != null)
		{
			if (buildingActor.IsDead)
				this.mcuDeployContext.Remove(mcuActor);
			else
				context.IsTransformed = true;
		}
	}

	private void DeployMcus(IBot bot, bool chooseLocation)
	{
		var newMcus = this.playerMcus.Actors
			.Where(a => a.IsIdle);

		foreach (var mcu in newMcus)
			this.DeployMcu(bot, mcu, chooseLocation);

		var deadMcuContexts = this.mcuDeployContext.Where(p => p.Key.IsDead).ToList();
		foreach (var (mcuActor, context) in deadMcuContexts)
		{
			if (context.IsTransformed)
			{
				if (context.BuildingActor != null)
					this.notifyMcuDeployment.ForEach(m => m.McuTransformed(bot, context.BuildingActor));

				this.mcuDeployContext.Remove(mcuActor);
			}
			else if (context.BuildingActor == null)
			{
				if (this.GetBuildingType(mcuActor) == BuildingType.Defense)
					this.defenseCenter = null;

				var newBuildingActor = mcuActor.ReplacedByActor;
				if (newBuildingActor != null)
				{
					context.BuildingActor = newBuildingActor;
					this.notifyMcuDeployment.ForEach(m => m.McuDeployed(bot, mcuActor, newBuildingActor));
				}
				else
				{
					this.mcuDeployContext.Remove(mcuActor);
				}
			}
		}
	}

	// Find any MCU and deploy them at a sensible location.
	private void DeployMcu(IBot bot, Actor mcu, bool move)
	{
		var deployLocation = mcu.Location;
		if (move)
		{
			var desiredLocation = this.ChooseMcuDeployLocation(mcu);
			if (desiredLocation == null)
				return;

			deployLocation = desiredLocation.Value;
		}
		else
		{
			// If the MCU has to move first, we can't be sure it reaches the destination alive, so we only
			// update base and defense center if the MCU is deployed immediately (i.e. at game start).
			foreach (var n in this.notifyPositionsUpdated)
			{
				n.UpdatedBaseCenter(mcu.Location);
				n.UpdatedDefenseCenter(mcu.Location);
			}
		}

		var context = this.mcuDeployContext.GetOrAdd(mcu, actor => new McuDeployContext { McuActor = actor });
		context.TargetLocation = deployLocation;
		context.DeployAttempt++;
		bot.QueueOrder(new Order(OrderConstants.MoveAndDeployTransformOrderID, mcu, Target.FromCell(this.world, deployLocation), true));

		this.notifyMcuDeployment.ForEach(m => m.OrderedMcuToDeploy(bot, mcu, deployLocation));
	}

	private CPos? ChooseMcuDeployLocation(Actor mcu)
	{
		var offset = mcu.Info.TraitInfo<TransformsInfo>().Offset;
		var buildingInfo = CustomBuildingInfoWrapper.WrapIfNecessary(McuUtils.GetTargetBuilding(this.world, mcu.Info)!);
		if (buildingInfo == null)
			return null;

		// Find the buildable cell that is closest to pos and centered around center
		CPos? FindPos(CPos center, CPos? target, int minRange, int maxRange)
		{
			var cells = this.world.Map.FindTilesInAnnulus(center, minRange, maxRange);

			// Sort by distance to target if we have one
			if (target != null)
			{
				var candidateCells = cells
					.OrderBy(c => (c - target.Value).LengthSquared)
					.ToList();

				// Use half of cells in the annulus, which are closest to target, as the candidate cells for the placement check.
				cells = candidateCells
					.Take(candidateCells.Count / 2)
					.Shuffle(this.world.LocalRandom);
			}
			else
				cells = cells.Shuffle(this.world.LocalRandom);

			return cells
				.Where(c => buildingInfo.CanPlaceBuilding(this.world, c + offset, null))
				.Shuffle(this.world.LocalRandom)
				.Cast<CPos?>().FirstOrDefault();
		}

		var maxDeployRetryCount = this.Info.MaxRetryCount;
		var deployContext = this.mcuDeployContext.GetOrAdd(mcu, actor => new McuDeployContext { McuActor = actor });
		if (deployContext.DeployAttempt.IsBetween(1, maxDeployRetryCount) && deployContext.TargetLocation != null)
		{
			return deployContext.TargetLocation;
		}

		var baseCenter = this.GetClosestBaseCenter(mcu.Location);

		var type = this.GetBuildingType(mcu);

		CPos? targetLocation = null;
		switch (type)
		{
			case BuildingType.Defense:
			{
				CPos searchCenter;
				CPos? targetCell;
				int minRadius, maxRadius;

				if (this.defenseCenter != null && this.mcuDeployContext.Count(p => this.GetBuildingType(p.Key) == BuildingType.Defense) < 3)
				{
					AIUtils.BotDebug($"Defense building deployment location: custom defense center ({this.defenseCenter})");

					searchCenter = this.defenseCenter.Value;
					targetCell = baseCenter;
					minRadius = this.Info.InnerDefenseRadius;

					if (deployContext.MaxMoveRadius == null)
						deployContext.MaxMoveRadius = this.Info.InnerDefenseRadius;
					else
						deployContext.MaxMoveRadius = Math.Min(deployContext.MaxMoveRadius.Value + this.Info.MoveRadiusIncreaseOnFailed, this.Info.MaximumDefenseRadius);

					maxRadius = deployContext.MaxMoveRadius.Value;
				}
				else if (this.world.LocalRandom.Next(100) < this.Info.PlaceDefenseTowardsEnemyChance)
				{
					AIUtils.BotDebug("Defense building deployment location: the defense perimeter");

					// Build near the closest enemy structure
					var closestEnemy = this.world.ActorsHavingTrait<Building>()
						.Where(a => !a.Disposed && this.player.RelationshipWith(a.Owner) == PlayerRelationship.Enemy)
						.ClosestToIgnoringPath(this.world.Map.CenterOfCell(baseCenter));

					searchCenter = baseCenter;
					targetCell = closestEnemy != null ? closestEnemy.Location : baseCenter;
					minRadius = this.Info.OuterDefenseRadius;
					if (deployContext.MaxMoveRadius == null)
						deployContext.MaxMoveRadius = this.Info.MaximumDefenseRadius;
					else
						deployContext.MaxMoveRadius = Math.Min(deployContext.MaxMoveRadius.Value + this.Info.MoveRadiusIncreaseOnFailed, this.Info.MaximumDefenseRadius);

					maxRadius = deployContext.MaxMoveRadius.Value;
				}
				else
				{
					AIUtils.BotDebug("Defense building deployment location: within base");

					searchCenter = baseCenter;
					targetCell = baseCenter;
					minRadius = this.Info.InnerDefenseRadius;
					maxRadius = this.Info.OuterDefenseRadius;
				}

				targetLocation = FindPos(searchCenter, targetCell, minRadius, maxRadius);

				break;
			}
			case BuildingType.Refinery:
			case BuildingType.Mine:
			{
				// If there's no economy manager, use generic algorithm for finding deploy location.
				if (this.economyManager == null)
				{
					if (deployContext.MaxMoveRadius == null)
						deployContext.MaxMoveRadius = this.Info.MinMoveRadius;
					else
						deployContext.MaxMoveRadius += this.Info.MoveRadiusIncreaseOnFailed;

					return FindPos(baseCenter, null, this.Info.MinBaseRadius, deployContext.MaxMoveRadius.Value);
				}

				deployContext.MaxMoveRadius ??= this.Info.MinMoveRadius;

				var deployZones = this.economyManager.GetDeployCellsCandidates(mcu, deployContext.MaxMoveRadius.Value);
				CPos? bestCell = null;
				var bestScore = int.MaxValue;
				foreach (var zone in deployZones)
				{
					foreach (var cell in zone.CandidateCells)
					{
						if (!buildingInfo.CanPlaceBuilding(this.world, cell + offset, null))
							continue;

						var distanceToBaseCenter = (cell - baseCenter).LengthSquared;
						if (distanceToBaseCenter < this.Info.MinMoveRadius.PowerOf2())
							continue;

						var distanceToPreferredLocation = (cell - zone.PreferredLocation).LengthSquared;

						var score = distanceToBaseCenter + distanceToPreferredLocation * 10;
						if (score < bestScore)
						{
							bestScore = score;
							bestCell = cell;
						}
					}
				}

				if (bestCell == null)
					deployContext.MaxMoveRadius = Math.Min(deployContext.MaxMoveRadius.Value + this.Info.MoveRadiusIncreaseOnFailed, this.Info.MaxMoveRadius);
				else
				{
					deployContext.MaxMoveRadius = this.Info.MinMoveRadius;
					targetLocation = bestCell;
				}

				break;
			}
			case BuildingType.Building:
			{
				var searchCenter = baseCenter;
				CPos? target = null;
				deployContext.MaxMoveRadius ??= this.Info.MinMoveRadius;
				var maxRadius = deployContext.MaxMoveRadius.Value;
				if (deployContext.DeployAttempt.IsBetween(maxDeployRetryCount, maxDeployRetryCount + 2))
				{
					searchCenter = mcu.Location;
					target = baseCenter;

					deployContext.DeployAttempt = 0;
				}

				targetLocation = FindPos(searchCenter, target, this.Info.MinMoveRadius, maxRadius);

				if (targetLocation == null)
					deployContext.MaxMoveRadius = Math.Min(deployContext.MaxMoveRadius.Value + this.Info.MoveRadiusIncreaseOnFailed, this.Info.MaxMoveRadius);
				else
					deployContext.MaxMoveRadius = this.Info.MinMoveRadius;

				break;
			}
			default:
			{
				targetLocation = FindPos(mcu.Location, mcu.Location, 0, deployContext.MaxMoveRadius ??= this.Info.MinMoveRadius);
				break;
			}
		}

		return targetLocation;
	}

	private BuildingType GetBuildingType(Actor mcu)
	{
		var mcuTypeName = mcu.Info.Name;

		if (this.Info.DefenseMcuTypes.Contains(mcuTypeName))
			return BuildingType.Defense;
		else if (this.Info.MineTypes.Contains(mcuTypeName))
			return BuildingType.Mine;
		else if (this.Info.RefineryTypes.Contains(mcuTypeName))
			return BuildingType.Refinery;

		return BuildingType.Building;
	}

	private CPos GetClosestBaseCenter(CPos mcuLocation)
	{
		var closestConstructionYard = this.constructionBuildings.Alive()
			.OrderBy(a => (mcuLocation - a.Location).LengthSquared)
			.FirstOrDefault();

		if (closestConstructionYard == null)
			return this.initialBaseCenter ?? GetRandomBuildingLocation() ?? CPos.Zero;

		var buildingInfo = this.constructionBuildingInfos[closestConstructionYard.Info.Name];

		return buildingInfo.GetCenterCellOfFootprint(this.world, closestConstructionYard.Location);

		CPos? GetRandomBuildingLocation()
		{
			return this.world.ActorsHavingTrait<BuildingInfo>().Where(a => a.Owner == this.player)
				.RandomOrDefault(this.world.LocalRandom)?.Location;
		}
	}

	void IBotRespondToAttack.RespondToAttack(IBot bot, Actor self, AttackInfo e)
	{
		if (e.Attacker == null || e.Attacker.Disposed)
			return;

		if (e.Attacker.Owner.RelationshipWith(self.Owner) != PlayerRelationship.Enemy)
			return;

		if (!e.Attacker.Info.HasTraitInfo<ITargetableInfo>())
			return;

		// Protect buildings
		if (self.Info.HasTraitInfo<BuildingInfo>())
			foreach (var n in this.notifyPositionsUpdated)
				n.UpdatedDefenseCenter(e.Attacker.Location);
	}

	//TODO:
	// 1. Build refinery and mine together
	// 2. Water terrain deploying

	List<MiniYamlNode>? IGameSaveTraitData.IssueTraitData(Actor self)
	{
		if (this.IsTraitDisabled)
			return null;

		return
		[
			new MiniYamlNode("InitialBaseCenter", FieldSaver.FormatValue(this.initialBaseCenter)),
			new MiniYamlNode("DefenseCenter", FieldSaver.FormatValue(this.defenseCenter))
		];
	}

	void IGameSaveTraitData.ResolveTraitData(Actor self, MiniYaml data)
	{
		if (self.World.IsReplay)
			return;

		var initialBaseCenterNode = data.NodeWithKeyOrDefault("InitialBaseCenter");
		if (initialBaseCenterNode != null)
			this.initialBaseCenter = FieldLoader.GetValue<CPos>("InitialBaseCenter", initialBaseCenterNode.Value.Value);

		var defenseCenterNode = data.NodeWithKeyOrDefault("DefenseCenter");
		if (defenseCenterNode != null)
			this.defenseCenter = FieldLoader.GetValue<CPos>("DefenseCenter", defenseCenterNode.Value.Value);
	}

	private class McuDeployContext
	{
		public required Actor McuActor { get; init; }

		public CPos? TargetLocation { get; set; }

		public Actor? BuildingActor { get; set; }

		public int DeployAttempt { get; set; }

		public int? MaxMoveRadius { get; set; }

		public bool IsTransformed { get; set; }
	}
}
