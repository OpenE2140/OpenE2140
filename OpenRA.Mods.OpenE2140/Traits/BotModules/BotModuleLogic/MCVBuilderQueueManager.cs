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
	sealed class MCVBuilderQueueManager
	{
		public readonly string Category;
		public int WaitTicks;

		readonly BaseMCVBuilderBotModule baseBuilder;
		readonly World world;
		readonly Player player;
		readonly PowerManager playerPower;
		readonly PlayerResources playerResources;

		Actor[] playerBuildings;
		Actor[] playerMCVs;
		int checkForBasesTicks;
		int cachedBases;
		int minimumExcessPower;

		bool itemQueuedThisTick = false;

		WaterCheck waterState = WaterCheck.NotChecked;

		public MCVBuilderQueueManager(BaseMCVBuilderBotModule baseBuilder, string category, Player p, PowerManager pm,
			PlayerResources pr)
		{
			this.baseBuilder = baseBuilder;
			world = p.World;
			player = p;
			playerPower = pm;
			playerResources = pr;
			Category = category;
			minimumExcessPower = baseBuilder.Info.MinimumExcessPower;
			if (baseBuilder.Info.NavalProductionTypes.Count == 0)
				waterState = WaterCheck.DontCheck;
		}

		public void Tick(IBot bot)
		{
			// If failed to place something N consecutive times, wait M ticks until resuming building production

			if (waterState == WaterCheck.NotChecked)
			{
				if (AIUtils.IsAreaAvailable<BaseProvider>(world, player, world.Map, baseBuilder.Info.MaxBaseRadius, baseBuilder.Info.WaterTerrainTypes))
					waterState = WaterCheck.EnoughWater;
				else
				{
					waterState = WaterCheck.NotEnoughWater;
					checkForBasesTicks = baseBuilder.Info.CheckForNewBasesDelay;
				}
			}

			if (waterState == WaterCheck.NotEnoughWater && --checkForBasesTicks <= 0)
			{
				var currentBases = world.ActorsHavingTrait<BaseProvider>().Count(a => a.Owner == player);

				if (currentBases > cachedBases)
				{
					cachedBases = currentBases;
					waterState = WaterCheck.NotChecked;
				}
			}

			// Only update once per second or so
			if (WaitTicks > 0)
				return;

			playerBuildings = world.ActorsHavingTrait<Building>().Where(a => a.Owner == player).ToArray();
			playerMCVs = world.ActorsHavingTrait<Transforms>().Where(a => a.Owner == player && baseBuilder.Info.MCVAndBuilding.ContainsKey(a.Info.Name)).ToArray();
			var excessPowerBonus = baseBuilder.Info.ExcessPowerIncrement * (playerBuildings.Length / baseBuilder.Info.ExcessPowerIncreaseThreshold.Clamp(1, int.MaxValue));
			minimumExcessPower = (baseBuilder.Info.MinimumExcessPower + excessPowerBonus).Clamp(baseBuilder.Info.MinimumExcessPower, baseBuilder.Info.MaximumExcessPower);

			// PERF: Queue only one actor at a time per category
			itemQueuedThisTick = false;
			var active = false;
			foreach (var queue in AIUtils.FindQueues(player, Category))
			{
				if (TickQueue(bot, queue))
					active = true;
			}

			// Add a random factor so not every AI produces at the same tick early in the game.
			// Minimum should not be negative as delays in HackyAI could be zero.
			var randomFactor = world.LocalRandom.Next(0, baseBuilder.Info.StructureProductionRandomBonusDelay);

			WaitTicks = active ? baseBuilder.Info.StructureProductionActiveDelay + randomFactor
				: baseBuilder.Info.StructureProductionInactiveDelay + randomFactor;
		}

		bool TickQueue(IBot bot, ProductionQueue queue)
		{
			// Waiting to build something
			if (queue.AllQueued().FirstOrDefault() == null)
			{
				// PERF: We shouldn't be queueing new units when we're low on cash
				if (playerResources.GetCashAndResources() < baseBuilder.Info.ProductionMinCashRequirement || itemQueuedThisTick)
					return false;

				var item = ChooseMCVToBuild(queue);
				if (item == null)
					return false;

				bot.QueueOrder(Order.StartProduction(queue.Actor, item.Name, 1));
				itemQueuedThisTick = true;
			}
			return true;
		}

		ActorInfo GetProducibleMCV(HashSet<string> actors, IEnumerable<ActorInfo> buildables, int buildAtleast = 0)
		{
			var enough = false;
			var available = buildables.Where(actor =>
			{
				// Are we able to build this?
				if (enough || !actors.Contains(actor.Name))
					return false;


				if (!baseBuilder.Info.MCVLimits.ContainsKey(actor.Name) && buildAtleast == 0)
					return true;

				var num = playerBuildings.Count(a => baseBuilder.Info.MCVAndBuilding.TryGetValue(actor.Name, out var n) && n == a.Info.Name) + playerMCVs.Count(a => a.Info.Name == actor.Name);

				if (buildAtleast > 0 && num >= buildAtleast)
				{
					enough = true;
					return false;
				}

				return num < (baseBuilder.Info.MCVLimits.TryGetValue(actor.Name, out var n) ? n : int.MaxValue);
			});

			return available.RandomOrDefault(world.LocalRandom);
		}

		bool HasSufficientPowerForBuilding(string name)
		{
			var actorInfo = world.Map.Rules.Actors[name];
			return playerPower == null || actorInfo.TraitInfos<PowerInfo>().Where(i => i.EnabledByDefault)
				.Sum(p => p.Amount) + playerPower.ExcessPower >= baseBuilder.Info.MinimumExcessPower;
		}

		ActorInfo ChooseMCVToBuild(ProductionQueue queue)
		{
			var buildableThings = queue.BuildableItems().ToList();

			// This gets used quite a bit, so let's cache it here
			var power = GetProducibleMCV(baseBuilder.Info.PowerTypes, buildableThings);

			// First priority is to get out of a low power situation
			if (playerPower != null && playerPower.ExcessPower < minimumExcessPower)
			{
				if (power != null && power.TraitInfos<PowerInfo>().Where(i => i.EnabledByDefault).Sum(p => p.Amount) > 0)
				{
					AIUtils.BotDebug("{0} decided to build {1}: Priority override (low power)", queue.Actor.Owner, power.Name);
					return power;
				}
			}

			// Next is to build up a strong economy
			var mine = GetProducibleMCV(baseBuilder.Info.MineTypes, buildableThings, 1);
			if (mine != null && HasSufficientPowerForBuilding(baseBuilder.Info.MCVAndBuilding[mine.Name]))
			{
				AIUtils.BotDebug("{0} decided to build {1}: Priority override (mine)", queue.Actor.Owner, mine.Name);
				return mine;
			}

			if (power != null && mine != null && !HasSufficientPowerForBuilding(baseBuilder.Info.MCVAndBuilding[mine.Name]))
			{
				AIUtils.BotDebug("{0} decided to build {1}: Priority override (would be low power)", queue.Actor.Owner, power.Name);
				return power;
			}

			// Next is to build up a strong economy
			var refinery = GetProducibleMCV(baseBuilder.Info.RefineryTypes, buildableThings, 1);
			if (refinery != null && HasSufficientPowerForBuilding(baseBuilder.Info.MCVAndBuilding[refinery.Name]))
			{
				AIUtils.BotDebug("{0} decided to build {1}: Priority override (refinery)", queue.Actor.Owner, refinery.Name);
				return refinery;
			}

			if (power != null && refinery != null && !HasSufficientPowerForBuilding(baseBuilder.Info.MCVAndBuilding[refinery.Name]))
			{
				AIUtils.BotDebug("{0} decided to build {1}: Priority override (would be low power)", queue.Actor.Owner, power.Name);
				return power;
			}

			// Make sure that we can spend as fast as we are earning
			if (baseBuilder.Info.NewProductionCashThreshold > 0 && playerResources.GetCashAndResources() > baseBuilder.Info.NewProductionCashThreshold)
			{
				var production = GetProducibleMCV(baseBuilder.Info.ProductionTypes, buildableThings);
				if (production != null && HasSufficientPowerForBuilding(baseBuilder.Info.MCVAndBuilding[production.Name]))
				{
					AIUtils.BotDebug("{0} decided to build {1}: Priority override (production)", queue.Actor.Owner, production.Name);
					return production;
				}

				if (power != null && production != null && !HasSufficientPowerForBuilding(baseBuilder.Info.MCVAndBuilding[production.Name]))
				{
					AIUtils.BotDebug("{0} decided to build {1}: Priority override (would be low power)", queue.Actor.Owner, power.Name);
					return power;
				}
			}

			// Only consider building this if there is enough water inside the base perimeter and there are close enough adjacent buildings
			if (waterState == WaterCheck.EnoughWater && baseBuilder.Info.NewProductionCashThreshold > 0
				&& playerResources.GetCashAndResources() > baseBuilder.Info.NewProductionCashThreshold
				&& AIUtils.IsAreaAvailable<GivesBuildableArea>(world, player, world.Map, baseBuilder.Info.CheckForWaterRadius, baseBuilder.Info.WaterTerrainTypes))
			{
				var navalproduction = GetProducibleMCV(baseBuilder.Info.NavalProductionTypes, buildableThings);
				if (navalproduction != null && HasSufficientPowerForBuilding(baseBuilder.Info.MCVAndBuilding[navalproduction.Name]))
				{
					AIUtils.BotDebug("{0} decided to build {1}: Priority override (navalproduction)", queue.Actor.Owner, navalproduction.Name);
					return navalproduction;
				}

				if (power != null && navalproduction != null && !HasSufficientPowerForBuilding(baseBuilder.Info.MCVAndBuilding[navalproduction.Name]))
				{
					AIUtils.BotDebug("{0} decided to build {1}: Priority override (would be low power)", queue.Actor.Owner, power.Name);
					return power;
				}
			}

			// Create some head room for resource storage if we really need it
			if (playerResources.Resources > 0.8 * playerResources.ResourceCapacity)
			{
				var silo = GetProducibleMCV(baseBuilder.Info.SiloTypes, buildableThings);
				if (silo != null && HasSufficientPowerForBuilding(baseBuilder.Info.MCVAndBuilding[silo.Name]))
				{
					AIUtils.BotDebug("{0} decided to build {1}: Priority override (silo)", queue.Actor.Owner, silo.Name);
					return silo;
				}

				if (power != null && silo != null && !HasSufficientPowerForBuilding(baseBuilder.Info.MCVAndBuilding[silo.Name]))
				{
					AIUtils.BotDebug("{0} decided to build {1}: Priority override (would be low power)", queue.Actor.Owner, power.Name);
					return power;
				}
			}

			// Build everything else
			foreach (var frac in baseBuilder.Info.MCVFractions.Shuffle(world.LocalRandom))
			{
				var name = frac.Key;

				// Does this building have initial delay, if so have we passed it?
				if (baseBuilder.Info.MCVDelays != null &&
					baseBuilder.Info.MCVDelays.TryGetValue(name, out var delay) &&
					delay > world.WorldTick)
					continue;

				// Can we build this structure?
				if (!buildableThings.Any(b => b.Name == name))
					continue;

				// Check the number of this structure and its variants
				var actorInfo = world.Map.Rules.Actors[name];
				var buildingName = baseBuilder.Info.MCVAndBuilding[name];
				var buildingInfo = world.Map.Rules.Actors[buildingName];

				var count = playerBuildings.Count(a => a.Info.Name == buildingName) + playerMCVs.Count(a => a.Info.Name == name) + (baseBuilder.MCVsBeingProduced.TryGetValue(name, out var num) ? num : 0);

				// Do we want to build this structure?
				if (count * 100 > frac.Value * playerBuildings.Length)
					continue;

				if (baseBuilder.Info.MCVLimits.TryGetValue(name, out var limit) && limit <= count)
					continue;

				// If we're considering to build a naval structure, check whether there is enough water inside the base perimeter
				// and any structure providing buildable area close enough to that water.
				// TODO: Extend this check to cover any naval structure, not just production.
				if (baseBuilder.Info.NavalProductionTypes.Contains(name)
					&& (waterState == WaterCheck.NotEnoughWater
						|| !AIUtils.IsAreaAvailable<GivesBuildableArea>(world, player, world.Map, baseBuilder.Info.CheckForWaterRadius, baseBuilder.Info.WaterTerrainTypes)))
					continue;

				// Will this put us into low power?
				var actor = world.Map.Rules.Actors[name];
				if (playerPower != null && (playerPower.ExcessPower < minimumExcessPower || !HasSufficientPowerForBuilding(baseBuilder.Info.MCVAndBuilding[actor.Name])))
				{
					// Try building a power plant instead
					if (power != null && power.TraitInfos<PowerInfo>().Where(i => i.EnabledByDefault).Sum(pi => pi.Amount) > 0)
					{
						if (playerPower.PowerOutageRemainingTicks > 0)
							AIUtils.BotDebug("{0} decided to build {1}: Priority override (is low power)", queue.Actor.Owner, power.Name);
						else
							AIUtils.BotDebug("{0} decided to build {1}: Priority override (would be low power)", queue.Actor.Owner, power.Name);

						return power;
					}
				}

				// Lets build this
				AIUtils.BotDebug("{0} decided to build {1}: Desired is {2} ({3} / {4}); current is {5} / {4}",
					queue.Actor.Owner, name, frac.Value, frac.Value * playerBuildings.Length, playerBuildings.Length, count);
				return actor;
			}

			// Too spammy to keep enabled all the time, but very useful when debugging specific issues.
			// AIUtils.BotDebug("{0} couldn't decide what to build for queue {1}.", queue.Actor.Owner, queue.Info.Group);
			return null;
		}
	}
}
