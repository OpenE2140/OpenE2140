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
	[Desc("Manages AI base construction.")]
	public class BaseMCVBuilderBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Tells the AI what building types are considered construction yards.")]
		public readonly HashSet<string> ConstructionYardTypes = new();

		[Desc("Tells the AI what building types are considered vehicle production facilities.")]
		public readonly HashSet<string> VehiclesFactoryTypes = new();

		[Desc("Tells the AI what building types are considered refineries.")]
		public readonly HashSet<string> RefineryTypes = new();

		[Desc("Tells the AI what building types are considered mines.")]
		public readonly HashSet<string> MineTypes = new();

		[Desc("Tells the AI what building types are considered power plants.")]
		public readonly HashSet<string> PowerTypes = new();

		[Desc("Tells the AI what building types are considered infantry production facilities.")]
		public readonly HashSet<string> BarracksTypes = new();

		[Desc("Tells the AI what building types are considered production facilities.")]
		public readonly HashSet<string> ProductionTypes = new();

		[Desc("Tells the AI what building types are considered naval production facilities.")]
		public readonly HashSet<string> NavalProductionTypes = new();

		[Desc("Tells the AI what building types are considered silos (resource storage).")]
		public readonly HashSet<string> SiloTypes = new();

		[Desc("Tells the AI what building types are considered defenses.")]
		public readonly HashSet<string> DefenseTypes = new();

		[Desc("Production queues AI uses for buildings.")]
		public readonly HashSet<string> BuildingQueues = new() { "Building" };

		[Desc("Production queues AI uses for defenses.")]
		public readonly HashSet<string> DefenseQueues = new() { "Defense" };

		[Desc("Minimum distance in cells from center of the base when checking for building placement.")]
		public readonly int MinBaseRadius = 2;

		[Desc("Radius in cells around the center of the base to expand.")]
		public readonly int MaxBaseRadius = 20;

		[Desc("Minimum excess power the AI should try to maintain.")]
		public readonly int MinimumExcessPower = 0;

		[Desc("The targeted excess power the AI tries to maintain cannot rise above this.")]
		public readonly int MaximumExcessPower = 0;

		[Desc("Increase maintained excess power by this amount for every ExcessPowerIncreaseThreshold of base buildings.")]
		public readonly int ExcessPowerIncrement = 0;

		[Desc("Increase maintained excess power by ExcessPowerIncrement for every N base buildings.")]
		public readonly int ExcessPowerIncreaseThreshold = 1;

		[Desc("Number of refineries to build before building a barracks.")]
		public readonly int InititalMinimumRefineryCount = 1;

		[Desc("Number of refineries to build additionally after building a barracks.")]
		public readonly int AdditionalMinimumRefineryCount = 1;

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

		[Desc("How many randomly chosen cells with resources to check when deciding refinery placement.")]
		public readonly int MaxResourceCellsToCheck = 3;

		[Desc("Delay (in ticks) until rechecking for new BaseProviders.")]
		public readonly int CheckForNewBasesDelay = 1500;

		[Desc("Chance that the AI will place the defenses in the direction of the closest enemy building.")]
		public readonly int PlaceDefenseTowardsEnemyChance = 100;

		[Desc("Minimum range at which to build defensive structures near a combat hotspot.")]
		public readonly int MinimumDefenseRadius = 5;

		[Desc("Maximum range at which to build defensive structures near a combat hotspot.")]
		public readonly int MaximumDefenseRadius = 20;

		[Desc("Try to build another production building if there is too much cash.")]
		public readonly int NewProductionCashThreshold = 5000;

		[Desc("Radius in cells around a factory scanned for rally points by the AI.")]
		public readonly int RallyPointScanRadius = 8;

		[Desc("Radius in cells around each building with ProvideBuildableArea",
			"to check for a 3x3 area of water where naval structures can be built.",
			"Should match maximum adjacency of naval structures.")]
		public readonly int CheckForWaterRadius = 8;

		[Desc("Terrain types which are considered water for base building purposes.")]
		public readonly HashSet<string> WaterTerrainTypes = new() { "Water" };

		[Desc("What buildings to the AI should build.", "What integer percentage of the total base must be this type of building.")]
		public readonly Dictionary<string, string> MCVAndBuilding = new();

		[Desc("What buildings to the AI should build.", "What integer percentage of the total base must be this type of building.")]
		public readonly Dictionary<string, int> MCVFractions = new();

		[Desc("What buildings should the AI have a maximum limit to build.")]
		public readonly Dictionary<string, int> MCVLimits = new();

		[Desc("When should the AI start building specific buildings.")]
		public readonly Dictionary<string, int> MCVDelays = new();

		[Desc("Only queue construction of a new structure when above this requirement.")]
		public readonly int ProductionMinCashRequirement = 500;

		public override object Create(ActorInitializer init) { return new BaseMCVBuilderBotModule(init.Self, this); }
	}

	public class BaseMCVBuilderBotModule : ConditionalTrait<BaseMCVBuilderBotModuleInfo>, IBotTick, IBotRequestPauseUnitProduction
	{
		// Actor, ActorCount.
		public Dictionary<string, int> MCVsBeingProduced = new();

		readonly World world;
		readonly Player player;
		PowerManager playerPower;
		PlayerResources playerResources;

		readonly MCVBuilderQueueManager[] builders;
		int currentBuilderIndex = 0;

		public BaseMCVBuilderBotModule(Actor self, BaseMCVBuilderBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
			builders = new MCVBuilderQueueManager[info.BuildingQueues.Count + info.DefenseQueues.Count];
		}

		protected override void Created(Actor self)
		{
			playerPower = self.Owner.PlayerActor.TraitOrDefault<PowerManager>();
			playerResources = self.Owner.PlayerActor.Trait<PlayerResources>();

			var i = 0;

			foreach (var building in Info.BuildingQueues)
				builders[i++] = new MCVBuilderQueueManager(this, building, player, playerPower, playerResources);

			foreach (var defense in Info.DefenseQueues)
				builders[i++] = new MCVBuilderQueueManager(this, defense, player, playerPower, playerResources);
		}

		bool IBotRequestPauseUnitProduction.PauseUnitProduction => !IsTraitDisabled;

		void IBotTick.BotTick(IBot bot)
		{
			MCVsBeingProduced.Clear();

			// PERF: We tick only one type of valid queue at a time
			// if AI gets enough cash, it can fill all of its queues with enough ticks
			var findQueue = false;
			for (int i = 0, builderIndex = currentBuilderIndex; i < builders.Length; i++)
			{
				if (++builderIndex >= builders.Length)
					builderIndex = 0;

				--builders[builderIndex].WaitTicks;

				var queues = AIUtils.FindQueues(player, builders[builderIndex].Category).ToArray();
				if (queues.Length != 0)
				{
					if (!findQueue)
					{
						currentBuilderIndex = builderIndex;
						findQueue = true;
					}

					// Refresh "BuildingsBeingProduced" only when AI can produce
					if (playerResources.GetCashAndResources() >= Info.ProductionMinCashRequirement)
					{
						foreach (var queue in queues)
						{
							var producing = queue.AllQueued().FirstOrDefault();
							if (producing == null)
								continue;

							if (MCVsBeingProduced.TryGetValue(producing.Item, out var number))
								MCVsBeingProduced[producing.Item] = number + 1;
							else
								MCVsBeingProduced.Add(producing.Item, 1);
						}
					}
				}
			}

			builders[currentBuilderIndex].Tick(bot);
		}
	}
}
