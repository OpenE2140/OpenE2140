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
using OpenRA.Mods.OpenE2140.Traits.BotModules.BotModuleLogic;
using OpenRA.Mods.OpenE2140.Traits.Mcu;
using OpenRA.Mods.OpenE2140.Traits.Power;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.BotModules;

[TraitLocation(SystemActors.Player)]
[Desc("Manages AI base construction.")]
public class BaseMcuBuilderBotModuleInfo : ConditionalTraitInfo
{
	[ActorReference]
	[Desc("Tells the AI what building types are considered power plants.")]
	public readonly HashSet<string> PowerTypes = [];

	[ActorReference]
	[Desc("Tells the AI what building types are considered research centers.")]
	public readonly HashSet<string> ResearchCenterTypes = [];

	[ActorReference]
	[Desc("Tells the AI what building types are considered production facilities.")]
	public readonly HashSet<string> ProductionTypes = [];

	[Desc("Production queues AI uses for buildings.")]
	public readonly HashSet<string> BuildingQueues = ["Building"];

	[Desc("Production queues AI uses for defenses.")]
	public readonly HashSet<string> DefenseQueues = ["Defense"];

	[Desc("Minimum excess power the AI should try to maintain.")]
	public readonly int MinimumExcessPower;

	[Desc("The targeted excess power the AI tries to maintain cannot rise above this.")]
	public readonly int MaximumExcessPower;

	[Desc("Increase maintained excess power by this amount for every ExcessPowerIncreaseThreshold of base buildings.")]
	public readonly int ExcessPowerIncrement;

	[Desc("Increase maintained excess power by ExcessPowerIncrement for every N base buildings.")]
	public readonly int ExcessPowerIncreaseThreshold = 1;

	[Desc("Maximum number of undeployed MCUs of one type. If greater than zero, there won't be more than N undeployed MCUs, " +
		"including MCUs currently being produced.")]
	public readonly int MaximumUndeployedMcu = 4;

	[Desc("Additional delay (in ticks) between structure production checks when there is no active production.",
		"StructureProductionRandomBonusDelay is added to this.")]
	public readonly int StructureProductionInactiveDelay = 125;

	[Desc("Additional delay (in ticks) added between structure production checks when actively building things.",
		"Note: this should be at least as large as the typical order latency to avoid duplicated build choices.")]
	public readonly int StructureProductionActiveDelay = 25;

	[Desc("A random delay (in ticks) of up to this is added to active/inactive production delays.")]
	public readonly int StructureProductionRandomBonusDelay = 10;

	[Desc("Delay (in ticks) until retrying to build structure after the last 3 consecutive attempts failed.")]
	public readonly int StructureProductionResumeDelay = 1500;

	[Desc("Try to build another production building if there is too much cash.")]
	public readonly int NewProductionCashThreshold = 5000;

	[Desc("Chance to build another production building if there is too much cash.")]
	public readonly int NewProductionChance = 50;

	[ActorReference(dictionaryReference: LintDictionaryReference.Keys)]
	[Desc("What buildings to the AI should build.", "What integer percentage of the total base must be this type of building.")]
	public readonly Dictionary<string, int> McuFractions = [];

	[ActorReference(dictionaryReference: LintDictionaryReference.Keys)]
	[Desc("What buildings should the AI have a maximum limit to build.")]
	public readonly Dictionary<string, int> McuLimits = [];

	[ActorReference(dictionaryReference: LintDictionaryReference.Keys)]
	[Desc("When should the AI start building specific buildings.")]
	public readonly Dictionary<string, int> McuDelays = [];

	[Desc("Only queue construction of a new structure when above this requirement.")]
	public readonly int ProductionMinCashRequirement = 500;

	public override object Create(ActorInitializer init) { return new BaseMcuBuilderBotModule(init.Self, this); }
}

public class BaseMcuBuilderBotModule : ConditionalTrait<BaseMcuBuilderBotModuleInfo>, IBotTick, IBotMcuBaseBuilder, INotifyActorDisposing
{
	private readonly OpenRA.World world;
	private readonly Player player;
	private readonly List<McuBuilderQueueManager> builders;
	private readonly LinkedList<string> queuedBuildRequests = [];

	private int currentBuilderIndex;

	// Actor type => ActorCount.
	public Dictionary<string, int> McusBeingProduced = [];

	public BaseMcuBuilderBotModule(Actor self, BaseMcuBuilderBotModuleInfo info)
		: base(info)
	{
		this.world = self.World;
		this.player = self.Owner;
		this.builders = new List<McuBuilderQueueManager>(info.BuildingQueues.Count + info.DefenseQueues.Count);
	}

	void IBotTick.BotTick(IBot bot)
	{
		this.InitializeQueueManagers();

		this.McusBeingProduced.Clear();

		// PERF: We tick only one type of valid queue at a time
		// if AI gets enough cash, it can fill all of its queues with enough ticks
		var findQueue = false;
		var builderIndex = this.currentBuilderIndex;
		for (var i = 0; i < this.builders.Count; i++)
		{
			if (++builderIndex >= this.builders.Count)
				builderIndex = 0;

			var builder = this.builders[builderIndex];
			--builder.WaitTicks;

			var queues = AIUtils.FindQueuesByCategory(this.player)[builder.Category]
				.Where(q => q.AnyItemsToBuild())
				.ToArray();
			if (queues.Length != 0)
			{
				if (!findQueue)
				{
					this.currentBuilderIndex = builderIndex;
					findQueue = true;
				}

				foreach (var queue in queues)
				{
					var producing = queue.AllQueued().FirstOrDefault();
					if (producing == null)
						continue;

					if (this.McusBeingProduced.TryGetValue(producing.Item, out var number))
						this.McusBeingProduced[producing.Item] = number + 1;
					else
						this.McusBeingProduced.Add(producing.Item, 1);
				}

				this.AddQueuedBuildings(builder, queues);
			}
		}

		this.builders[this.currentBuilderIndex].Tick(bot);
	}

	private void InitializeQueueManagers()
	{
		var queues = this.Info.BuildingQueues
			.Concat(this.Info.DefenseQueues);
		if (!queues.Any() || this.builders.Count > 0)
			return;

		var economyManager = this.player.PlayerActor.TraitsImplementing<IBotEconomyManager>().FirstEnabledTraitOrDefault();
		var playerPower = this.player.PlayerActor.TraitOrDefault<PowerManagerBase>();
		var playerResources = this.player.PlayerActor.Trait<PlayerResources>();

		foreach (var queue in queues)
			this.builders.Add(new McuBuilderQueueManager(this, economyManager, queue, this.player, playerPower, playerResources));
	}

	private void AddQueuedBuildings(McuBuilderQueueManager builder, ProductionQueue[] queues)
	{
		var queue = queues.FirstOrDefault(q => q.Info.Type == builder.Category);

		// Check, if it's even possible to queue specified actor in current builder
		if (queue == null || builder.Category != queue.Info.Type)
			return;

		var current = this.queuedBuildRequests.First;
		while (current != null)
		{
			var next = current.Next;

			var mcuActor = this.GetMcuFromActor(current.Value);
			if (mcuActor != null
				&& mcuActor.TryGetTrait<BuildableInfo>(out var buildableInfo)
				&& buildableInfo.Queue.Contains(builder.Category))
			{
				builder.RequestBuildingProduction(mcuActor.Name);

				this.queuedBuildRequests.Remove(current);
			}

			current = next;
		}
	}

	void IBotMcuBaseBuilder.RequestBuildingProduction(IBot bot, string actorName)
	{
		this.queuedBuildRequests.AddLast(actorName);
	}

	int IBotMcuBaseBuilder.RequestedProductionCount(IBot bot, string actorName)
	{
		var mcuActor = this.GetMcuFromActor(actorName);
		if (mcuActor == null)
			return 0;

		var buildableInfo = mcuActor.TraitInfoOrDefault<BuildableInfo>();
		if (buildableInfo == null)
			return 0;

		var queuedActorsInBuilders = buildableInfo
			.Queue
			.Sum(category => this.builders.FirstOrDefault(m => m.Category == category)?.RequestedProductionCount(actorName) ?? 0);

		var queuedActorsInModule = this.queuedBuildRequests.Count(a => a == actorName);

		return queuedActorsInBuilders + queuedActorsInModule;
	}

	int IBotMcuBaseBuilder.InProductionCount(IBot bot, string actor)
	{
		return this.McusBeingProduced.Where(p => p.Key == actor).Sum(p => p.Value);
	}

	private ActorInfo? GetMcuFromActor(string actorName)
	{
		var actorInfo = this.world.Map.Rules.Actors[actorName];
		if (actorInfo.HasTraitInfo<McuInfo>())
			return actorInfo;

		return McuUtils.GetMcuActor(this.world, actorInfo);
	}

	void INotifyActorDisposing.Disposing(Actor self)
	{
		foreach (var builder in this.builders)
		{
			builder.Dispose();
		}
	}
}
