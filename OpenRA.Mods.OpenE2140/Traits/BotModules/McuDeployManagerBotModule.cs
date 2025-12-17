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
using OpenRA.Mods.OpenE2140.Traits.Mcu;
using OpenRA.Traits;
using TransformsInfo = OpenRA.Mods.OpenE2140.Traits.Mcu.TransformsInfo;

namespace OpenRA.Mods.OpenE2140.Traits.BotModules;

[TraitLocation(SystemActors.Player)]
[Desc("Manages AI MCUs.")]
public class McuDeployManagerBotModuleInfo : ConditionalTraitInfo
{
	[Desc("Actor types that are considered MCUs, which deploy into normal buildings.")]
	public readonly HashSet<string> McuTypes = [];

	[Desc("Actor types that are considered MCUs, which deploy into defense buildings.")]
	public readonly HashSet<string> DefenseMcuTypes = [];

	[Desc("Delay (in ticks) between looking for and giving out orders to new MCUs.")]
	public readonly int ScanForNewMcuInterval = 30;

	[Desc("Minimum distance in cells from center of the base when checking for MCU deployment location.")]
	public readonly int MinMoveRadius = 4;

	[Desc("If not possible to find a place to deploy, increase move radius by this amount until a suitable place is found.")]
	public readonly int MoveRadiusIncreaseOnFailed = 4;

	[Desc("Maximum distance in cells from center of the base when checking for MCU deployment location.",
		"When successful find a location, move radius will become MinMoveRadius.")]
	public readonly int MaxMoveRadius = 20;

	public override object Create(ActorInitializer init) { return new McuDeployManagerBotModule(init.Self, this); }
}

public class McuDeployManagerBotModule : ConditionalTrait<McuDeployManagerBotModuleInfo>, IBotTick, IBotPositionsUpdated, IGameSaveTraitData, IBotRespondToAttack
{
	private readonly OpenRA.World world;
	private readonly Player player;
	private readonly ActorIndex.OwnerAndNamesAndTrait<McuInfo> playerMcus;

	private IBotPositionsUpdated[] notifyPositionsUpdated = [];
	private CPos initialBaseCenter;
	private CPos defenseCenter;

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
			var desiredLocation = this.ChooseMcuDeployLocation(buildingInfo, transformsInfo.Offset, mcu.Location);
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

	private CPos? ChooseMcuDeployLocation(ActorInfo actorInfo, CVec offset, CPos location)
	{
		var buildingInfo = CustomBuildingInfoWrapper.WrapIfNecessary(actorInfo);
		if (buildingInfo == null)
			return null;

		// Find the buildable cell that is closest to pos and centered around center
		CPos? FindPos(CPos center, CPos target, int minRange, int maxRange)
		{
			var cells = this.world.Map.FindTilesInAnnulus(center, minRange, maxRange);

			// Sort by distance to target if we have one
			if (center != target)
				cells = cells.OrderBy(c => (c - target).LengthSquared);
			else
				cells = cells.Shuffle(this.world.LocalRandom);

			return cells
				.Where(c => buildingInfo.CanPlaceBuilding(this.world, c + offset, null))
				.Shuffle(this.world.LocalRandom)
				.Cast<CPos?>().FirstOrDefault();
		}

		var loc = this.Info.DefenseMcuTypes.Contains(actorInfo.Name) && this.defenseCenter != CPos.Zero ? location : this.defenseCenter;
		return FindPos(loc, loc, 0, this.moveRadius);
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
