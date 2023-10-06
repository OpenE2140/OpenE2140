#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("Manages AI MCVs.")]
	public class McvDeployManagerBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Actor types that are considered MCVs (deploy into building).")]
		public readonly HashSet<string> McvTypes = new();

		[Desc("Actor types that are considered MCVs that deploy into defence.")]
		public readonly HashSet<string> DefenceMcvTypes = new();
		
		[Desc("Delay (in ticks) between looking for and giving out orders to new MCVs.")]
		public readonly int ScanForNewMcvInterval = 30;

		[Desc("Minimum distance in cells from center of the base when checking for MCV deployment location.")]
		public readonly int MinMoveRadius = 4;

		[Desc("When fail to find a place to deploy, move radius increased this amount until we find a suitable place.")]
		public readonly int MoveRadiusInceasedOnFailed = 4;

		[Desc("Maximum distance in cells from center of the base when checking for MCV deployment location.",
			"When successful find a location, move radius will become MinMoveRadius.")]
		public readonly int MaxMoveRadius = 20;

		public override object Create(ActorInitializer init) { return new McvDeployManagerBotModule(init.Self, this); }
	}

	public class McvDeployManagerBotModule : ConditionalTrait<McvDeployManagerBotModuleInfo>, IBotTick, IBotPositionsUpdated, IGameSaveTraitData, IBotRespondToAttack
	{
		readonly World world;
		readonly Player player;

		IBotPositionsUpdated[] notifyPositionsUpdated;

		CPos initialBaseCenter;
		public CPos DefenseCenter { get; private set; }
		int scanInterval;
		int moveRadius;
		bool firstTick = true;

		public McvDeployManagerBotModule(Actor self, McvDeployManagerBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
			moveRadius = info.MinMoveRadius;
		}

		protected override void Created(Actor self)
		{
			notifyPositionsUpdated = self.Owner.PlayerActor.TraitsImplementing<IBotPositionsUpdated>().ToArray();
		}

		protected override void TraitEnabled(Actor self)
		{
			// Avoid all AIs reevaluating assignments on the same tick, randomize their initial evaluation delay.
			scanInterval = world.LocalRandom.Next(Info.ScanForNewMcvInterval, Info.ScanForNewMcvInterval * 2);
		}

		void IBotPositionsUpdated.UpdatedBaseCenter(CPos newLocation)
		{
			initialBaseCenter = newLocation;
		}

		void IBotPositionsUpdated.UpdatedDefenseCenter(CPos newLocation)
		{
			DefenseCenter = newLocation;
		}

		void IBotTick.BotTick(IBot bot)
		{
			if (firstTick)
			{
				DeployMcvs(bot, false);
				firstTick = false;
			}

			if (--scanInterval <= 0)
			{
				scanInterval = Info.ScanForNewMcvInterval;
				DeployMcvs(bot, true);
			}
		}

		void DeployMcvs(IBot bot, bool chooseLocation)
		{
			var newMCVs = world.ActorsHavingTrait<Transforms>()
				.Where(a => a.Owner == player && a.IsIdle && Info.McvTypes.Contains(a.Info.Name));

			foreach (var mcv in newMCVs)
				DeployMcv(bot, mcv, chooseLocation);
		}

		// Find any MCV and deploy them at a sensible location.
		void DeployMcv(IBot bot, Actor mcv, bool move)
		{
			if (move)
			{
				var transformsInfo = mcv.Info.TraitInfo<TransformsInfo>();
				var desiredLocation = ChooseMcvDeployLocation(transformsInfo.IntoActor, transformsInfo.Offset, mcv.Location);
				if (desiredLocation == null)
				{
					moveRadius = Math.Min(moveRadius + Info.MoveRadiusInceasedOnFailed, Info.MaxMoveRadius);
					return;
				}
				else
					moveRadius = Info.MinMoveRadius;

				bot.QueueOrder(new Order("Move", mcv, Target.FromCell(world, desiredLocation.Value), true));
			}

			// If the MCV has to move first, we can't be sure it reaches the destination alive, so we only
			// update base and defense center if the MCV is deployed immediately (i.e. at game start).
			foreach (var n in notifyPositionsUpdated)
			{
				n.UpdatedBaseCenter(mcv.Location);
				n.UpdatedDefenseCenter(mcv.Location);
			}

			bot.QueueOrder(new Order("DeployTransform", mcv, true));
		}

		CPos? ChooseMcvDeployLocation(string actorType, CVec offset, CPos location)
		{
			var actorInfo = world.Map.Rules.Actors[actorType];
			var bi = actorInfo.TraitInfoOrDefault<BuildingInfo>();
			if (bi == null)
				return null;

			// Find the buildable cell that is closest to pos and centered around center
			CPos? FindPos(CPos center, CPos target, int minRange, int maxRange)
			{
				var cells = world.Map.FindTilesInAnnulus(center, minRange, maxRange);

				// Sort by distance to target if we have one
				if (center != target)
					cells = cells.OrderBy(c => (c - target).LengthSquared);
				else
					cells = cells.Shuffle(world.LocalRandom);

				foreach (var cell in cells)
					if (world.CanPlaceBuilding(cell + offset, actorInfo, bi, null))
						return cell;

				return null;
			}

			var loc = Info.DefenceMcvTypes.Contains(actorType) && DefenseCenter != CPos.Zero ? location : DefenseCenter;
			return FindPos(loc, loc, 0, moveRadius);
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
				foreach (var n in notifyPositionsUpdated)
					n.UpdatedDefenseCenter(e.Attacker.Location);
		}

		//TODO:
		// 1. Build refinery and mine together
		// 2. Water terrain deploying

		List<MiniYamlNode> IGameSaveTraitData.IssueTraitData(Actor self)
		{
			if (IsTraitDisabled)
				return null;

			return new List<MiniYamlNode>()
			{
				new MiniYamlNode("InitialBaseCenter", FieldSaver.FormatValue(initialBaseCenter)),
				new MiniYamlNode("DefenseCenter", FieldSaver.FormatValue(DefenseCenter))
			};
		}

		void IGameSaveTraitData.ResolveTraitData(Actor self, MiniYaml data)
		{
			if (self.World.IsReplay)
				return;

			var initialBaseCenterNode = data.NodeWithKeyOrDefault("InitialBaseCenter");
			if (initialBaseCenterNode != null)
				initialBaseCenter = FieldLoader.GetValue<CPos>("InitialBaseCenter", initialBaseCenterNode.Value.Value);

			var defenseCenterNode = data.NodeWithKeyOrDefault("DefenseCenter");
			if (defenseCenterNode != null)
				DefenseCenter = FieldLoader.GetValue<CPos>("DefenseCenter", defenseCenterNode.Value.Value);
		}
	}
}
