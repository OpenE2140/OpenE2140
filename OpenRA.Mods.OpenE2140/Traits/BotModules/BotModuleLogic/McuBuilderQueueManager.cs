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
using OpenRA.Mods.OpenE2140.Traits.Power;
using OpenRA.Mods.OpenE2140.Utils;
using OpenRA.Traits;
using Transforms = OpenRA.Mods.OpenE2140.Traits.Mcu.Transforms;

namespace OpenRA.Mods.OpenE2140.Traits.BotModules.BotModuleLogic;

public sealed class McuBuilderQueueManager : IDisposable
{
	public string Category { get; }

	// Wait a bit, before activating the main logic (fixes bug, when bot builds Power Plant despite already having one from start):
	// - tick 0 = actors/traits are created and actors with Power traits are registered into PowerManager
	// - tick 1 = PowerManager updates its state
	// - tick 2 = McuBuilderQueueManager activates and can see that the bot has enough power (if that's the case), thus fixing the bug.
	public int WaitTicks = 2;

	private readonly BaseMcuBuilderBotModule baseBuilder;
	private readonly OpenRA.World world;
	private readonly Player player;
	private readonly PowerManagerBase playerPower;
	private readonly PlayerResources playerResources;

	private readonly OwnerAndTraitIndex<BuildingInfo> playerBuildings;
	private readonly OwnerAndTraitIndex<McuInfo> playerMcus;

	private int minimumExcessPower;
	private bool itemQueuedThisTick;

	public McuBuilderQueueManager(
		BaseMcuBuilderBotModule baseBuilder,
		string category,
		Player player,
		PowerManagerBase powerManager,
		PlayerResources playerResources)
	{
		this.baseBuilder = baseBuilder;
		this.Category = category;
		this.world = player.World;
		this.player = player;
		this.playerPower = powerManager;
		this.playerResources = playerResources;
		this.minimumExcessPower = baseBuilder.Info.MinimumExcessPower;
		this.playerBuildings = new OwnerAndTraitIndex<BuildingInfo>(this.world, [], this.player);
		this.playerMcus = new OwnerAndTraitIndex<McuInfo>(this.world, [], this.player);
	}

	public void Tick(IBot bot)
	{
		// Only update once per second or so
		if (this.WaitTicks > 0)
			return;

		var excessPowerBonus = this.baseBuilder.Info.ExcessPowerIncrement *
			(this.playerBuildings.Alive().Count() / this.baseBuilder.Info.ExcessPowerIncreaseThreshold.Clamp(1, int.MaxValue));
		this.minimumExcessPower = (this.baseBuilder.Info.MinimumExcessPower + excessPowerBonus)
			.Clamp(this.baseBuilder.Info.MinimumExcessPower, this.baseBuilder.Info.MaximumExcessPower);

		// PERF: Queue only one actor at a time per category
		this.itemQueuedThisTick = false;
		var active = false;
		foreach (var queue in AIUtils.FindQueuesByCategory(this.player)[this.Category])
		{
			if (!queue.Enabled)
				continue;

			if (this.TickQueue(bot, queue))
				active = true;
		}

		// Add a random factor so not every AI produces at the same tick early in the game.
		// Minimum should not be negative as delays in HackyAI could be zero.
		var randomFactor = this.world.LocalRandom.Next(0, this.baseBuilder.Info.StructureProductionRandomBonusDelay);

		this.WaitTicks = active ? this.baseBuilder.Info.StructureProductionActiveDelay + randomFactor
			: this.baseBuilder.Info.StructureProductionInactiveDelay + randomFactor;
	}

	private bool TickQueue(IBot bot, ProductionQueue queue)
	{
		// Waiting to build something
		if (queue.AllQueued().FirstOrDefault() == null)
		{
			// PERF: We shouldn't be queueing new units when we're low on cash
			if (this.playerResources.Cash < this.baseBuilder.Info.ProductionMinCashRequirement || this.itemQueuedThisTick)
				return false;

			var item = this.ChooseMcuToBuild(queue);
			if (item == null)
				return false;

			bot.QueueOrder(Order.StartProduction(queue.Actor, item.Name, 1));
			this.itemQueuedThisTick = true;
		}
		return true;
	}

	private ActorInfo? ChooseMcuToBuild(ProductionQueue queue)
	{
		var buildableThings = queue.BuildableItems().ToList();

		// This gets used quite a bit, so let's cache it here
		var powerMcu = this.GetProducibleMcu(this.baseBuilder.Info.PowerTypes, buildableThings);
		var powerBuilding = McuUtils.GetTargetBuilding(this.world, powerMcu);

		// First priority is to get out of a low power situation
		if (this.playerPower != null && this.playerPower.Power <= this.minimumExcessPower)
		{
			return PickMcuToBuild(powerMcu, powerBuilding);
		}

		// Bootstrap technology research by building Research Center
		var researchCenterMcu = this.GetProducibleMcu(this.baseBuilder.Info.ResearchCenterTypes, buildableThings, 1);
		var researchCenter = McuUtils.GetTargetBuilding(this.world, researchCenterMcu);
		if (researchCenter != null && this.HasSufficientPowerForBuilding(researchCenter))
		{
			AIUtils.BotDebug("{0} decided to build {1}: Priority override (research center)", queue.Actor.Owner, researchCenterMcu?.Name);
			return researchCenterMcu;
		}

		if (powerMcu != null && researchCenter != null && !this.HasSufficientPowerForBuilding(researchCenter))
		{
			AIUtils.BotDebug("{0} decided to build {1}: Priority override (would be low power)", queue.Actor.Owner, powerMcu.Name);
			return powerMcu;
		}

		// Make sure that we can spend as fast as we are earning
		if (this.baseBuilder.Info.NewProductionCashThreshold > 0 && this.playerResources.Cash > this.baseBuilder.Info.NewProductionCashThreshold
			&& this.world.LocalRandom.Next(100) < this.baseBuilder.Info.NewProductionChance)
		{
			var productionMcu = this.GetProducibleMcu(this.baseBuilder.Info.ProductionTypes, buildableThings);
			var building = McuUtils.GetTargetBuilding(this.world, productionMcu);
			if (building != null && this.HasSufficientPowerForBuilding(building))
			{
				AIUtils.BotDebug("{0} decided to build {1}: Priority override (production)", queue.Actor.Owner, productionMcu?.Name);
				return productionMcu;
			}

			if (powerMcu != null && building != null && !this.HasSufficientPowerForBuilding(building))
				return PickMcuToBuild(powerMcu, powerBuilding);
		}

		// TODO: add support for Water Base

		// Build everything else
		foreach (var frac in this.baseBuilder.Info.McuFractions.Shuffle(this.world.LocalRandom))
		{
			var mcuName = frac.Key;

			// Does this building have initial delay, if so have we passed it?
			if (this.baseBuilder.Info.McuDelays != null &&
				this.baseBuilder.Info.McuDelays.TryGetValue(mcuName, out var delay) &&
				delay > this.world.WorldTick)
				continue;

			// Can we build this structure?
			if (!buildableThings.Any(b => b.Name == mcuName))
				continue;

			// Check the number of this structure and its variants
			var mcuActorInfo = this.world.Map.Rules.Actors[mcuName];
			var buildingInfo = McuUtils.GetTargetBuilding(this.world, mcuActorInfo)!;

			// Keep total count of in progress buildings to a reasonable number (if enabled).
			var existingMcuCount = this.playerMcus.Alive().Count(a => a.Info.Name == mcuName)
				+ this.baseBuilder.McusBeingProduced.GetValueOrDefault(mcuName);
			if (this.baseBuilder.Info.MaximumUndeployedMcu > 0 && existingMcuCount > this.baseBuilder.Info.MaximumUndeployedMcu)
				continue;

			var count = this.playerBuildings.Alive().Count(a => a.Info.Name == buildingInfo.Name) + existingMcuCount;

			// Do we want to build this structure?
			if (count * 100 > frac.Value * this.playerBuildings.Alive().Count())
				continue;

			if (this.baseBuilder.Info.McuLimits.TryGetValue(mcuName, out var limit) && limit <= count)
				continue;

			// Will this put us into low power?
			if (this.playerPower != null && (this.playerPower.Power < this.minimumExcessPower || !this.HasSufficientPowerForBuilding(buildingInfo)))
			{
				return PickMcuToBuild(powerMcu, powerBuilding);
			}

			// Lets build this
			AIUtils.BotDebug("{0} decided to build {1}", queue.Actor.Owner, mcuName);
			return mcuActorInfo;
		}

		// Too spammy to keep enabled all the time, but very useful when debugging specific issues.
		// AIUtils.BotDebug("{0} couldn't decide what to build for queue {1}.", queue.Actor.Owner, queue.Info.Group);
		return null;

		ActorInfo? PickMcuToBuild(ActorInfo? mcu, ActorInfo? building, string? reason = null)
		{
			if (mcu != null
				&& !this.playerMcus.Alive().Any(a => a.Info.Name == mcu.Name)
				&& !this.playerBuildings.Alive().Any(a => a.Info == building && !a.Trait<ProvidesPrerequisite>().IsTraitEnabled()))
			{
				AIUtils.BotDebug($"{queue.Actor.Owner} decided to build {mcu.Name}: {reason ?? "Priority override (low power)"}");
				return mcu;
			}

			// Waiting for the MCU to deploy
			return null;
		}
	}

	private ActorInfo? GetProducibleMcu(HashSet<string> actors, IEnumerable<ActorInfo> buildables, int buildAtLeast = 0)
	{
		// TODO: rewrite: Where() shouldn't depend on outside side effect
		var enough = false;
		var available = buildables.Where(actor =>
		{
			// Are we able to build this?
			if (enough || !actors.Contains(actor.Name))
				return false;

			if (!this.baseBuilder.Info.McuLimits.ContainsKey(actor.Name) && buildAtLeast == 0)
				return true;

			var totalCount = this.playerBuildings.Alive().Count(a => McuUtils.GetMcuActor(this.world, a.Info)?.Name == actor.Name);
			var mcuCount = this.playerMcus.Alive().Count(a => a.Info.Name == actor.Name);

			if (mcuCount > 0 || (buildAtLeast > 0 && totalCount >= buildAtLeast))
			{
				enough = true;
				return false;
			}

			return totalCount < (this.baseBuilder.Info.McuLimits.TryGetValue(actor.Name, out var n) ? n : int.MaxValue);
		});

		return available.RandomOrDefault(this.world.LocalRandom);
	}

	private bool HasSufficientPowerForBuilding(ActorInfo actorInfo)
	{
		return Helpers.HasSufficientPowerForBuilding(this.playerPower, actorInfo, this.minimumExcessPower);
	}

	public void Dispose()
	{
		this.playerMcus.Dispose();
		this.playerBuildings.Dispose();
	}
}
