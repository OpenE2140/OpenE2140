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

	[Desc("Actor types that are considered MCUs, which deploy into defense buildings.")]
	public readonly HashSet<string> DefenseMcuTypes = [];

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

	public override object Create(ActorInitializer init) { return new McuDeployManagerBotModule(init.Self, this); }

	public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
	{
		var nonExistingActors = this.ConstructionBuildingTypes.Where(a => !rules.Actors.ContainsKey(a)).ToList();
		if (nonExistingActors.Count > 0)
			throw new YamlException($"Unknown actors: {string.Join(", ", nonExistingActors)}");

		base.RulesetLoaded(rules, ai);
	}
}

public class McuDeployManagerBotModule : ConditionalTrait<McuDeployManagerBotModuleInfo>, IBotTick, IBotPositionsUpdated, IGameSaveTraitData, IBotRespondToAttack
{
	private readonly OpenRA.World world;
	private readonly Player player;
	private readonly ActorIndex.OwnerAndNamesAndTrait<McuInfo> playerMcus;
	private readonly ActorIndex.OwnerAndNamesAndTrait<BuildingInfo> constructionBuildings;

	private IBotPositionsUpdated[] notifyPositionsUpdated = [];

	private CPos? initialBaseCenter;
	private CPos? defenseCenter;

	private int scanInterval;
	private int moveRadius;
	private bool firstTick = true;

	public McuDeployManagerBotModule(Actor self, McuDeployManagerBotModuleInfo info)
		: base(info)
	{
		this.world = self.World;
		this.player = self.Owner;
		this.moveRadius = info.MinMoveRadius;

		this.playerMcus = new ActorIndex.OwnerAndNamesAndTrait<McuInfo>(this.world, this.Info.McuTypes, this.player);
		this.constructionBuildings = new ActorIndex.OwnerAndNamesAndTrait<BuildingInfo>(this.world, this.Info.ConstructionBuildingTypes, this.player);
	}

	protected override void Created(Actor self)
	{
		this.notifyPositionsUpdated = self.Owner.PlayerActor.TraitsImplementing<IBotPositionsUpdated>().ToArray();
	}

	protected override void TraitEnabled(Actor self)
	{
		// Avoid all AIs reevaluating assignments on the same tick, randomize their initial evaluation delay.
		this.scanInterval = this.world.LocalRandom.Next(this.Info.ScanForNewMcuInterval, this.Info.ScanForNewMcuInterval * 2);
	}

	void IBotPositionsUpdated.UpdatedBaseCenter(CPos newLocation)
	{
		this.initialBaseCenter = newLocation;
		this.defenseCenter = newLocation;
	}

	void IBotPositionsUpdated.UpdatedDefenseCenter(CPos newLocation)
	{
	}

	public int UndeployedMcuCount(IBot bot, string mcuType)
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
	}

	private void DeployMcus(IBot bot, bool chooseLocation)
	{
		var newMcus = this.playerMcus.Actors
			.Where(a => a.IsIdle);

		foreach (var mcu in newMcus)
			this.DeployMcu(bot, mcu, chooseLocation);
	}

	// Find any MCU and deploy them at a sensible location.
	private void DeployMcu(IBot bot, Actor mcu, bool move)
	{
		if (move)
		{
			var transformsInfo = mcu.Info.TraitInfo<TransformsInfo>();
			var buildingInfo = McuUtils.GetTargetBuilding(this.world, mcu.Info)!;

			var type = BuildingType.Building;
			if (this.Info.DefenseMcuTypes.Contains(mcu.Info.Name))
				type = BuildingType.Defense;

			var desiredLocation = this.ChooseMcuDeployLocation(buildingInfo, type, transformsInfo.Offset, mcu.Location);
			if (desiredLocation == null)
			{
				this.moveRadius = Math.Min(this.moveRadius + this.Info.MoveRadiusIncreaseOnFailed, this.Info.MaxMoveRadius);
				return;
			}
			else
				this.moveRadius = this.Info.MinMoveRadius;

			bot.QueueOrder(new Order("Move", mcu, Target.FromCell(this.world, desiredLocation.Value), true));
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

		bot.QueueOrder(new Order("DeployTransform", mcu, true));
	}

	private CPos? ChooseMcuDeployLocation(ActorInfo actorInfo, BuildingType type, CVec offset, CPos mcuLocation)
	{
		var buildingInfo = CustomBuildingInfoWrapper.WrapIfNecessary(actorInfo);
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

		var baseCenter = this.GetClosestBaseCenter(mcuLocation);

		switch (type)
		{
			case BuildingType.Defense:
			{
				CPos targetCell;
				int minRadius, maxRadius;

				if (this.world.LocalRandom.Next(100) < this.Info.PlaceDefenseTowardsEnemyChance)
				{
					AIUtils.BotDebug("Defense building deployment: pick at the defense perimeter");

					// Build near the closest enemy structure
					var closestEnemy = this.world.ActorsHavingTrait<Building>()
						.Where(a => !a.Disposed && this.player.RelationshipWith(a.Owner) == PlayerRelationship.Enemy)
						.ClosestToIgnoringPath(this.world.Map.CenterOfCell(this.defenseCenter ?? baseCenter));

					targetCell = closestEnemy != null ? closestEnemy.Location : baseCenter;
					minRadius = this.Info.OuterDefenseRadius;
					maxRadius = this.Info.MaximumDefenseRadius;
				}
				else
				{
					AIUtils.BotDebug("Defense building deployment: pick within base");

					minRadius = this.Info.InnerDefenseRadius;
					maxRadius = this.Info.OuterDefenseRadius;
					targetCell = this.defenseCenter ?? baseCenter;
				}

				return FindPos(baseCenter, targetCell, minRadius, maxRadius);
			}
			case BuildingType.Building:
			{
				return FindPos(baseCenter, null, this.Info.MinBaseRadius, this.moveRadius);
			}
		}

		return FindPos(mcuLocation, mcuLocation, 0, this.moveRadius);
	}

	private CPos GetClosestBaseCenter(CPos mcuLocation)
	{
		var closestConstructionYard = this.constructionBuildings.Alive()
			.OrderBy(a => (mcuLocation - a.Location).LengthSquared)
			.FirstOrDefault();

		return closestConstructionYard?.Location ?? this.initialBaseCenter
			?? this.world.Actors.Where(a => a.Owner == this.player)
			.RandomOrDefault(this.world.LocalRandom).Location;
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
}
