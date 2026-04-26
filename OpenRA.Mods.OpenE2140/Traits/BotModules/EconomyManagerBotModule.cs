using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Mods.OpenE2140.Traits.BotModules.BotModuleLogic;
using OpenRA.Mods.OpenE2140.Traits.BotModules.Extensions;
using OpenRA.Mods.OpenE2140.Traits.Mcu;
using OpenRA.Mods.OpenE2140.Traits.Resources;
using OpenRA.Traits;
using TransformsInfo = OpenRA.Mods.OpenE2140.Traits.Mcu.TransformsInfo;

namespace OpenRA.Mods.OpenE2140.Traits.BotModules;

[TraitLocation(SystemActors.Player)]
[Desc("Manages economy for a bot.")]
public class EconomyManagerBotModuleInfo : ConditionalTraitInfo, NotBefore<IResourceLayerInfo>
{
	[ActorReference]
	[Desc("Actor types that are considered crate transporters. If crate transporters count drops below RefineryTypes count, a new crate transporters is built.",
		"Leave empty to disable crate transporters replacement. Currently only needed by crate transporter replacement system.")]
	public readonly HashSet<string> CrateTransporterTypes = [];

	[ActorReference]
	[Desc("Tells the AI what building types are considered refineries.")]
	public readonly HashSet<string> RefineryTypes = [];

	[ActorReference]
	[Desc("Tells the AI what building types are considered mines.")]
	public readonly HashSet<string> MineTypes = [];

	[Desc("List of delays in ticks between each time the AI should expand their economy.",
		"List count determines maximum economy level. Bot stops expansions," +
		$"when total number of Mines/Refineries reaches Count({nameof(EconomyExpansionDelays)}) + 1.",
		"(the first expansion starts when the game starts (i.e. without delay).")]
	public readonly List<int> EconomyExpansionDelays = [];

	[Desc("After bot reaches maximum economy level, this controls delay between additional economy expansions (if > 0).",
		"In this case, the bot will keep expanding economy until there are no more places to deploy Mine.",
		"If null or <= 0, additional extra expansions are disabled.")]
	public readonly int? ExtraExpansionDelay;

	[Desc("Bot will perform additional economy expansions only if its income (per 1 minute) is less than this value.")]
	public readonly int ExtraExpansionMaxIncome = 10000;

	[Desc("Interval (in ticks) between performing the module logic.")]
	public readonly int LogicInterval = 50;

	[Desc("Minimum number of resource cells, which the Mine building's footprint must cover for acceptable MCU deployment.")]
	public readonly int MinimumResourceCellsToDeploy = 3;

	[Desc("Minimum number of adjacent resource cells that are required to consider this cluster, when deciding Mine location deployment.")]
	public readonly int ResourceCellClusterMinimumCount = 3;

	[Desc("Radius around Mine MCU location, where possible locations for Mine are checked, when deciding Mine location deployment.")]
	public readonly int MaxResourceCellSearchRadius = 15;

	[Desc("Minimum distance from Mine, where Refinery can be placed.")]
	public readonly int MinRefineryDistance = 5;

	[Desc("Maximum distance from Mine, where Refinery can be placed.")]
	public readonly int MaxRefineryDistance = 12;

	[Desc("Number of crate transporters assigned to single Mine/Refinery pair.",
		"Be aware of the fact, that over the course of the game, AI can have more crate transporters: " +
		"for example Refinery gets destroyed, then rebuilt -> AI gets one free crate transporter from the Refinery.")]
	public readonly int CrateTransporterPerRefineryMinePair = 2;

	[FieldLoader.Ignore]
	private Lazy<McuBuildingMap>? mineMcuBuildingMap;
	public McuBuildingMap MineMcuBuildingMap => this.mineMcuBuildingMap?.Value ?? new McuBuildingMap();

	[FieldLoader.Ignore]
	private Lazy<McuBuildingMap>? refineryMcuBuildingMap;
	public McuBuildingMap RefineryMcuBuildingMap => this.refineryMcuBuildingMap?.Value ?? new McuBuildingMap();

	public override object Create(ActorInitializer init) { return new EconomyManagerBotModule(init.Self, this); }

	public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
	{
		base.RulesetLoaded(rules, ai);

		if (this.MineTypes.Count == 0)
			throw new YamlException($"At least one actor type has to be defined in {nameof(this.MineTypes)}");

		if (this.RefineryTypes.Count == 0)
			throw new YamlException($"At least one actor type has to be defined in {nameof(this.RefineryTypes)}");

		if (this.CrateTransporterTypes.Count == 0)
			throw new YamlException($"At least one actor type has to be defined in {nameof(this.CrateTransporterTypes)}");

		this.mineMcuBuildingMap = Exts.Lazy(() => McuBuildingMap.Create(rules, this.MineTypes));
		this.refineryMcuBuildingMap = Exts.Lazy(() => McuBuildingMap.Create(rules, this.RefineryTypes));
	}

	public class McuBuildingMap
	{
		public List<McuBuildingMapping> Mappings { get; init; } = [];

		public IEnumerable<ActorInfo> McuActors => this.Mappings.Select(m => m.McuActor);
		public IEnumerable<ActorInfo> BuildingActors => this.Mappings.Select(m => m.BuildingActor);

		public bool HasMcuMapping(ActorInfo mcuActor)
		{
			return this.Mappings.Any(m => m.McuActor == mcuActor);
		}

		public bool HasBuildingMapping(ActorInfo buildingActor)
		{
			return this.Mappings.Any(m => m.BuildingActor == buildingActor);
		}

		public McuBuildingMapping? GetByMcu(ActorInfo mcuActor)
		{
			return this.Mappings.FirstOrDefault(m => m.McuActor == mcuActor);
		}

		public McuBuildingMapping? GetByBuilding(ActorInfo buildingActor)
		{
			return this.Mappings.FirstOrDefault(m => m.BuildingActor == buildingActor);
		}

		public ActorInfo? GetBuildingByMcu(ActorInfo mcuActor)
		{
			return this.GetByMcu(mcuActor)?.BuildingActor;
		}

		public ActorInfo? GetMcuByBuilding(ActorInfo buildingActor)
		{
			return this.GetByBuilding(buildingActor)?.McuActor;
		}

		public static McuBuildingMap Create(Ruleset rules, IReadOnlyCollection<string> buildingActorNames)
		{
			var list = new List<McuBuildingMapping>(buildingActorNames.Count);

			foreach (var buildingActorName in buildingActorNames)
			{
				if (!rules.Actors.TryGetValue(buildingActorName, out var buildingActor))
					continue;

				var mcuActor = McuUtils.GetMcuActor(rules, buildingActor);
				if (mcuActor == null)
					continue;

				list.Add(new McuBuildingMapping(mcuActor, buildingActor));
			}

			return new McuBuildingMap { Mappings = list };
		}
	}

	public class McuBuildingMapping
	{
		public ActorInfo McuActor { get; }
		public ActorInfo BuildingActor { get; }
		public ICustomBuildingInfo? BuildingInfo { get; }

		public CVec[] FootprintOffsets { get; } = [];

		public CVec TransformOffset { get; }

		public McuBuildingMapping(ActorInfo mcuActor, ActorInfo buildingActor)
		{
			this.McuActor = mcuActor;
			this.BuildingActor = buildingActor;
			this.TransformOffset = mcuActor.TraitInfo<TransformsInfo>().Offset;
			this.BuildingInfo = CustomBuildingInfoWrapper.WrapIfNecessary(this.BuildingActor);

			if (this.BuildingInfo != null)
				this.FootprintOffsets = this.BuildingInfo.Footprint().Keys.Select(v => v + this.TransformOffset).ToArray();
		}
	}
}

public class EconomyManagerBotModule : ConditionalTrait<EconomyManagerBotModuleInfo>, IBotTick, INotifyActorDisposing,
	IBotEconomyManager, IBotRequestPauseUnitProduction, IBotMcuDeployment
{
	private static readonly int SufficientIncome = 2000;

	private readonly OpenRA.World world;
	private readonly Player player;
	private readonly ActorIndex.OwnerAndNamesAndTrait<CrateTransporterInfo> crateTransporters;
	private readonly ActorIndex.OwnerAndNamesAndTrait<ResourceMineInfo> mines;
	private readonly ActorIndex.OwnerAndNamesAndTrait<ResourceRefineryInfo> refineries;
	private readonly List<MineRefineryAssignment> mineRefineryAssignments = [];
	private readonly (List<Actor> Mines, List<Actor> Refineries) assignmentActorsDirtyCheck = ([], []);
	private readonly List<(Actor mcuActor, CPos deployLocation)> mineMcusMovingToDeploy = [];

	private IBotRequestUnitProduction[] requestUnitProduction = [];
	private IBotMcuBaseBuilder[] mcuBaseBuilder = [];
	private IBotMcuDeployManager[] mcuDeployManager = [];
	private IResourceLayer? resourceLayer;
	private PlayerIncomeTracker? incomeTracker;
	private ResourceMineDeployZoneSearch? resourceMineDeployZoneSearch;

	private IBotMcuDeployManager? McuDeployManager => this.mcuDeployManager.FirstEnabledTraitOrDefault();

	private int logicTicks;
	private bool hasSufficientEconomy;
	private int reachedEconomyLevel;
	private int? nextExpansionTick = 0;
	private int? lastEconomyExpansion;
	private int? economyExpansionTargetLevel;
	private int? expandingEconomySince;

	internal IReadOnlyList<MineRefineryAssignment> MineRefineryAssignments => this.mineRefineryAssignments;

	public EconomyManagerBotModule(Actor self, EconomyManagerBotModuleInfo info)
		: base(info)
	{
		this.world = self.World;
		this.player = self.Owner;
		this.crateTransporters = new ActorIndex.OwnerAndNamesAndTrait<CrateTransporterInfo>(this.world, info.CrateTransporterTypes, this.player);
		this.mines = new ActorIndex.OwnerAndNamesAndTrait<ResourceMineInfo>(this.world, info.MineTypes, this.player);
		this.refineries = new ActorIndex.OwnerAndNamesAndTrait<ResourceRefineryInfo>(this.world, info.RefineryTypes, this.player);
	}

	protected override void Created(Actor self)
	{
		this.requestUnitProduction = this.player.PlayerActor.TraitsImplementing<IBotRequestUnitProduction>().ToArray();
		this.mcuBaseBuilder = this.player.PlayerActor.TraitsImplementing<IBotMcuBaseBuilder>().ToArray();
		this.mcuDeployManager = this.player.PlayerActor.TraitsImplementing<IBotMcuDeployManager>().ToArray();
		this.resourceLayer = self.World.WorldActor.TraitOrDefault<IResourceLayer>();

		var playerResources = this.player.PlayerActor.TraitOrDefault<PlayerResources>();
		if (playerResources != null)
			this.incomeTracker = new PlayerIncomeTracker(self.World, playerResources);

		if (this.resourceLayer != null)
			this.resourceMineDeployZoneSearch = new ResourceMineDeployZoneSearch(this.world.Map, this.resourceLayer, this.Info);
	}

	protected override void TraitEnabled(Actor self)
	{
		// Avoid all AIs running their logic the same tick, randomize their initial scan delay.
		this.logicTicks = this.world.LocalRandom.Next(this.Info.LogicInterval);
	}

	void IBotTick.BotTick(IBot bot)
	{
		this.incomeTracker?.Tick();

		if (--this.logicTicks > 0)
			return;

		this.logicTicks = this.Info.LogicInterval;

		this.CleanTrackedMcuDeployments();
		this.UpdateMineRefineryAssignments();
		this.OrderCrateTransporterToWork(bot);

		var hadSufficientEconomy = this.hasSufficientEconomy;
		this.Tick(bot);

		if (!hadSufficientEconomy && this.hasSufficientEconomy)
		{
			//AIUtils.BotDebug("{0} has sufficient economy", bot.Player);

			// Force reassigning mines/refineries, in case crate transporters got out of sync.
			this.assignmentActorsDirtyCheck.Mines.Clear();
			this.assignmentActorsDirtyCheck.Refineries.Clear();
		}
	}

	private void CleanTrackedMcuDeployments()
	{
		this.mineMcusMovingToDeploy.RemoveAll(t => t.mcuActor.IsDead);
	}

	private void UpdateMineRefineryAssignments()
	{
		var unassignedMines = this.mines.Alive().ToHashSet();
		var unassignedRefineries = this.refineries.Alive().ToHashSet();

		var hasChanged = false;
		if (!unassignedMines.SetEquals(this.assignmentActorsDirtyCheck.Mines))
			hasChanged = true;

		if (!hasChanged && !unassignedRefineries.SetEquals(this.assignmentActorsDirtyCheck.Refineries))
			hasChanged = true;

		if (!hasChanged)
			return;

		this.assignmentActorsDirtyCheck.Mines.Clear();
		this.assignmentActorsDirtyCheck.Mines.AddRange(unassignedMines);
		this.assignmentActorsDirtyCheck.Refineries.Clear();
		this.assignmentActorsDirtyCheck.Refineries.AddRange(unassignedRefineries);

		if (unassignedMines.Count == 0 || unassignedRefineries.Count == 0)
		{
			this.mineRefineryAssignments.Clear();
			return;
		}

		// Create lookup of existing, valid assignment pairs to preserve existing connections
		var validAssignmentPairs = this.mineRefineryAssignments
			.Where(a => a.Mine?.IsDead == false && a.Refinery?.IsDead == false)
			.ToDictionary(a => (a.Mine, a.Refinery));
		this.mineRefineryAssignments.Clear();
		this.mineRefineryAssignments.EnsureCapacity(Math.Max(unassignedMines.Count, unassignedRefineries.Count));

		foreach (var mine in unassignedMines)
		{
			Actor? nearestRefinery = null;
			var maxSearchRadius = this.Info.MaxRefineryDistance;
			for (var i = 0; i <= 3; i++)
			{
				var searchResult = FindNearestActor(unassignedRefineries, mine.Location, maxSearchRadius * i);
				if (searchResult?.actor == null)
				{
					++maxSearchRadius;
					continue;
				}

				nearestRefinery = searchResult.Value.actor;
				break;
			}

			if (nearestRefinery != null)
			{
				if (validAssignmentPairs.TryGetValue((mine, nearestRefinery), out var assignment))
				{
					assignment.RemoveInvalidCrateTransporters();
				}
				else
				{
					assignment = new MineRefineryAssignment
					{
						Mine = mine,
						Refinery = nearestRefinery,
						ExpectedCrateTransporterCount = this.Info.CrateTransporterPerRefineryMinePair
					};
				}

				this.mineRefineryAssignments.Add(assignment);

				unassignedRefineries.Remove(nearestRefinery);
			}

			static (Actor actor, int distance)? FindNearestActor(IEnumerable<Actor> actors, CPos searchStart, int maxRadius)
			{
				return actors
					.Select(a => (actor: a, distance: (a.Location - searchStart).LengthSquared))
					.OrderBy(t => t.distance)
					.FirstOrDefault(a => a.distance <= maxRadius.PowerOf2());
			}
		}
	}

	private void OrderCrateTransporterToWork(IBot bot)
	{
		var availableCrateTransporters = this.crateTransporters.Alive().ToHashSet();
		if (availableCrateTransporters.Count == 0)
			return;

		// First pass: skip those crate transporters, which are already assigned
		foreach (var assignment in this.mineRefineryAssignments)
		{
			for (var i = assignment.CrateTransporters.Count - 1; i >= 0; i--)
			{
				var crateTransporter = assignment.CrateTransporters[i];

				availableCrateTransporters.Remove(crateTransporter);

				// Clean up any dead crate transporters
				if (crateTransporter.IsDead)
					assignment.CrateTransporters.RemoveAt(i);
			}
		}

		// Second pass: queue orders for crate transporters and assign those, which are free (i.e. currently unassigned)
		foreach (var assignment in this.mineRefineryAssignments)
		{
			assignment.OrderCrateTransportersToWork(bot, availableCrateTransporters);
		}
	}

	bool IBotRequestPauseUnitProduction.PauseUnitProduction
	{
		get
		{
			// No crate transporters -> never pause production
			if (this.crateTransporters.Actors.Count == 0)
				return false;

			// Not expanding economy -> can produce units
			if (this.expandingEconomySince == null)
				return false;

			// Expanding economy for short period of time -> can produce units
			var expansionTime = this.world.WorldTick - this.expandingEconomySince;
			if (expansionTime <= 100)
				return false;

			// Enough cash -> no need to pause unit production
			if (this.incomeTracker?.CurrentCash > 5000)
				return false;

			// Expanding stalled for some time -> alternate between paused and resumed production
			if (expansionTime / 100 % 2 == 1)
				return true;

			// Sufficient income -> can produce units
			if (this.incomeTracker?.Income > SufficientIncome * 2)
				return false;

			// Insufficient income -> don't produce new units
			return true;
		}
	}

	void IBotMcuDeployment.OrderedMcuToDeploy(IBot bot, Actor mcuActor, CPos deployLocation)
	{
		if (this.Info.MineMcuBuildingMap.HasMcuMapping(mcuActor.Info))
			this.mineMcusMovingToDeploy.Add((mcuActor, deployLocation));
	}

	void IBotMcuDeployment.McuDeployed(IBot bot, Actor mcuActor, Actor buildingActor)
	{
		this.mineMcusMovingToDeploy.RemoveAll(t => mcuActor == t.mcuActor);
	}

	private void Tick(IBot bot)
	{
		// 1) Retrieve necessary hard data about the current state of the economy.
		var mcuBaseBuilder = this.mcuBaseBuilder.FirstEnabledTraitOrDefault();
		var currentIncome = this.incomeTracker?.Income ?? 0;

		var mineCount = this.mines.Alive().Count();
		var refineryCount = this.refineries.Alive().Count();
		var crateTransporterCount = this.crateTransporters.Alive().Count();

		// Take into account Refineries currently built or their deployment is in progress
		// (because Refinery provides one free crate transporter)
		// TODO: unhardcode, look at the Refinery actor currently being built and check if it will create free transporter
		var refineriesCurrentlyBuilt = mcuBaseBuilder != null ?
			GetProductionInProgressCount(bot, mcuBaseBuilder, this.Info.RefineryMcuBuildingMap.McuActors) : 0;

		// ??? Maybe add count of transporters in production?
		var predictedTransporterCount = crateTransporterCount + refineriesCurrentlyBuilt;

		// 2) Determine current economy level, previous expansion state and whether the economy is sufficient.
		var currentEconomyLevel = Math.Min(mineCount, refineryCount);
		var targetEconomyLevel = Math.Max(mineCount, refineryCount);
		var isExpanding = this.expandingEconomySince != null;

		var isEconomySufficient = false;
		if (this.economyExpansionTargetLevel != null)
		{
			if (this.economyExpansionTargetLevel <= currentEconomyLevel)
			{
				// Expansion finished
				this.reachedEconomyLevel = Math.Max(this.economyExpansionTargetLevel.Value, this.reachedEconomyLevel);
				if (this.reachedEconomyLevel < this.Info.EconomyExpansionDelays.Count)
					AIUtils.BotDebug($"{bot.Player} has expanded economy to level {targetEconomyLevel} (highest: {this.reachedEconomyLevel})");
				else if (currentEconomyLevel < this.reachedEconomyLevel)
					AIUtils.BotDebug($"{bot.Player} is rebuilding economy (current: {currentEconomyLevel}, highest: {this.reachedEconomyLevel}).");
				else if (this.Info.ExtraExpansionDelay > 0)
					AIUtils.BotDebug($"{bot.Player} has completed all economy expansion milestones, but might do some extra expansions later.");
				else
					AIUtils.BotDebug($"{bot.Player} has completed all economy expansion milestones and is now in maintenance mode.");

				this.lastEconomyExpansion = this.world.WorldTick;

				isEconomySufficient = true;
				this.economyExpansionTargetLevel = null;
				this.expandingEconomySince = null;
			}
			else if (this.economyExpansionTargetLevel == currentEconomyLevel + 1)
			{
				targetEconomyLevel = this.economyExpansionTargetLevel.Value;
			}
			else
			{
				// Economy expansion target level does not match current level +1.
				// This likely means that economy has degraded, so abort the expansion and let economy recover.
				this.economyExpansionTargetLevel = null;
				this.expandingEconomySince = null;
			}
		}
		else if (this.nextExpansionTick != null && this.nextExpansionTick < this.world.WorldTick)
		{
			if (currentEconomyLevel != targetEconomyLevel)
			{
				// Economy degraded just before the expansion should have started.
				// Delay new expansion to let economy recover.
				this.nextExpansionTick += 100;
			}
			else
			{
				// Reached tick for next expansion: remember when it started.
				AIUtils.BotDebug("{0} started expanding its economy", bot.Player);
				this.expandingEconomySince = this.world.WorldTick;
				targetEconomyLevel = currentEconomyLevel + 1;
				this.economyExpansionTargetLevel = targetEconomyLevel;
				this.nextExpansionTick = null;
				isExpanding = true;
			}
		}
		else
		{
			// Maintenance mode (i.e. not expanding economy)
			isEconomySufficient = currentEconomyLevel == targetEconomyLevel;
		}

		var expectedCurrentTransporterCount = currentEconomyLevel * this.Info.CrateTransporterPerRefineryMinePair;
		var shouldBuildCrateTransporter = predictedTransporterCount < expectedCurrentTransporterCount
			&& mineCount > 0
			&& refineryCount > 0;
		if (shouldBuildCrateTransporter)
		{
			isEconomySufficient = false;
		}

		// 3. Perform economy maintenance or expansion actions

		// Expand or rebuild Mine/Refinery only when there's enough crate transporters on current enconomy level.
		// Rationale: if there's not enough crate transporters for current number of Mines/Refineries,
		// it does not make sense to build more Mines/Refineries.
		if (predictedTransporterCount >= expectedCurrentTransporterCount)
		{
			// Not enough mines -> build more
			if (mineCount < targetEconomyLevel)
			{
				if (mcuBaseBuilder != null && TryRequestProduction(bot, mcuBaseBuilder, this.Info.MineMcuBuildingMap.McuActors))
					return;
			}

			// Not enough refineries -> build more
			if (refineryCount < targetEconomyLevel)
			{
				if (mcuBaseBuilder != null && TryRequestProduction(bot, mcuBaseBuilder, this.Info.RefineryMcuBuildingMap.McuActors))
					return;
			}
		}

		//var isEconomySufficient = currentEconomyLevel >= targetEconomyLevel;

		// Not enough crate transporters -> build more
		var unitBuilder = this.requestUnitProduction.FirstEnabledTraitOrDefault();
		if (unitBuilder != null && shouldBuildCrateTransporter)
		{
			// Build the best crate transporter that's possible to build currently.
			var crateTransporterType = this.Info.CrateTransporterTypes
				.LastOrDefault(t => unitBuilder.CanBuildUnit(this.player, t));

			if (crateTransporterType != null
				&& unitBuilder.RequestedProductionCount(bot, crateTransporterType) == 0
				&& unitBuilder.InProductionCount(this.player, crateTransporterType) == 0)
				unitBuilder.RequestUnitProduction(bot, crateTransporterType);

			//// Expansion is complete only if there's at least 1 crate transporter
			//isEconomySufficient = predictedTransporterCount > 0;
		}

		// 4. Plan next expansion (if possible).

		// Maybe add check for sufficient income here to unblock McuBuilderQueueManager?
		if (isEconomySufficient && this.economyExpansionTargetLevel == null && this.nextExpansionTick == null)
		{
			var minDelayBetweenExpansions = 300;

			if (targetEconomyLevel == 0)
			{
				// Economy level is down to zero; start expansion immediately.
				this.nextExpansionTick = this.world.WorldTick;
			}
			else if (targetEconomyLevel < this.reachedEconomyLevel)
			{
				// Economy has degraded, expand again
				this.nextExpansionTick = this.world.WorldTick + minDelayBetweenExpansions;
			}
			else if (targetEconomyLevel < this.Info.EconomyExpansionDelays.Count + 1)
			{
				var nextExpansionDelay = this.Info.EconomyExpansionDelays[targetEconomyLevel - 1];

				this.nextExpansionTick = this.world.WorldTick + Math.Min(minDelayBetweenExpansions, nextExpansionDelay);
			}
			else if (this.Info.ExtraExpansionDelay > 0 && this.incomeTracker?.Income < this.Info.ExtraExpansionMaxIncome)
			{
				var min = Math.Max(minDelayBetweenExpansions, this.Info.ExtraExpansionDelay.Value - 100);
				var max = Math.Max(min, this.Info.ExtraExpansionDelay.Value) + 100;
				this.nextExpansionTick = this.world.LocalRandom.Next(min, max);
			}
			//else if (this.nextExpansionTick == null && targetEconomyLevel < this.reachedEconomyLevel)
			//{
			//	// Economy has degraded, try expanding in a short while again to recover.
			//	this.nextExpansionTick = this.world.WorldTick + 100;
			//}

			//this.expandingEconomySince = null;
		}

		this.hasSufficientEconomy = isEconomySufficient;

		// TODO: other checks

		bool TryRequestProduction(IBot bot, IBotMcuBaseBuilder mcuBaseBuilder, IEnumerable<ActorInfo?> mcuActors)
		{
			var validMcuActors = mcuActors.OfType<ActorInfo>();
			if (GetProductionInProgressCount(bot, mcuBaseBuilder, validMcuActors) > 0)
				return false;

			var mcu = validMcuActors.Random(this.world.LocalRandom);

			mcuBaseBuilder.RequestBuildingProduction(bot, mcu.Name);
			return true;
		}

		int GetProductionInProgressCount(IBot bot, IBotMcuBaseBuilder mcuBaseBuilder, IEnumerable<ActorInfo?> mcuActors)
		{
			return mcuActors
				.OfType<ActorInfo>()
				.Sum(mcu => mcuBaseBuilder.RequestedProductionCount(bot, mcu.Name) +
					mcuBaseBuilder.InProductionCount(bot, mcu.Name) +
					(this.McuDeployManager?.UndeployedMcuCount(bot, mcu.Name) ?? 0));
		}
	}

	public bool HasSufficientEconomy()
	{
		return this.hasSufficientEconomy;
	}

	public List<DeployZone> GetDeployCellsCandidates(Actor mcu)
	{
		if (!McuUtils.TryGetTargetBuilding(this.world, mcu.Info, out var building))
			return [];

		// Both Mine and Refinery have different placement requirements
		if (this.Info.MineTypes.Contains(building.Name))
		{
			return this.FindResourceDeployZones(mcu);
		}
		else if (this.Info.RefineryTypes.Contains(building.Name))
		{
			return this.FindRefineryDeployZones(mcu, building);
		}

		return [];
	}

	private List<DeployZone> FindRefineryDeployZones(Actor mcu, ActorInfo building)
	{
		var buildingInfo = CustomBuildingInfoWrapper.WrapIfNecessary(building);
		if (buildingInfo == null)
			return [];

		if (!mcu.Info.TryGetTrait<TransformsInfo>(out var transformsInfo))
			return [];

		// Origin = where the building's top-left would be if MCU stayed at its current location.
		var origin = mcu.Location + transformsInfo.Offset;

		// Find nearby Mines, which are:
		// - alive
		// - and have currently unassigned Mine

		var possibleMineLocations = new List<CPos>();

		foreach (var mine in this.mines.Alive())
		{
			if (this.mineRefineryAssignments.Any(a => a.Mine == mine && a.Refinery != null))
				continue;

			var mineBuildingInfo = CustomBuildingInfoWrapper.WrapIfNecessary(mine.Info);
			if (mineBuildingInfo == null)
				continue;

			var mineCenterLocation = this.world.Map.CellContaining(mineBuildingInfo.GetCenterOfFootprint(mine.Location));

			var mineMcuInfo = McuUtils.GetMcuActor(this.world, mine.Info);
			if (mineMcuInfo == null || !mineMcuInfo.TryGetTrait<TransformsInfo>(out var mineTransformsInfo))
				continue;

			possibleMineLocations.Add(mine.Location - mineTransformsInfo.Offset);
		}

		// If there's a mine MCU currently moving to deploy, consider the deploy cell a possible Mine location.
		// TODO: add more logic for various situations: multiple Refinery MCUs, enemy attacking (should avoid sending Refinery MCU), etc.
		foreach (var (mcuActor, deployLocation) in this.mineMcusMovingToDeploy)
		{
			if (!mcuActor.IsDead)
				possibleMineLocations.Add(deployLocation);
		}

		var checkedLocations = new HashSet<CPos>();
		var candidateZones = new List<DeployZone>();

		// Evaluate each mine for suitable location, where new Refinery can be placed
		foreach (var location in possibleMineLocations)
		{
			var deployZoneCells = new List<CPos>();

			foreach (var cell in this.world.Map.FindTilesInAnnulus(location, this.Info.MinRefineryDistance, this.Info.MaxRefineryDistance))
			{
				if (checkedLocations.Add(cell) && this.world.Map.Contains(cell) && buildingInfo.IsCellBuildable(this.world, cell))
					deployZoneCells.Add(cell);
			}

			var zone = new DeployZone
			{
				PreferredLocation = location,
				CandidateCells = deployZoneCells
			};
			candidateZones.Add(zone);
		}

		return candidateZones;
	}

	private List<DeployZone> FindResourceDeployZones(Actor mcu)
	{
		if (this.resourceMineDeployZoneSearch == null)
			return [];

		// After all economic milestones have been reached, expand search radius further away from outside of the base
		var maxSearchRadius = this.Info.MaxResourceCellSearchRadius;
		if (this.reachedEconomyLevel >= this.Info.EconomyExpansionDelays.Count + 1)
			maxSearchRadius *= 2;

		var mineMcuMapping = this.Info.MineMcuBuildingMap.GetByMcu(mcu.Info);
		if (mineMcuMapping == null)
			return [];

		return this.resourceMineDeployZoneSearch.FindResourceMineDeployZones(mcu.Location, maxSearchRadius, mineMcuMapping.FootprintOffsets);
	}

	void INotifyActorDisposing.Disposing(Actor self)
	{
		this.crateTransporters.Dispose();
		this.refineries.Dispose();
		this.mines.Dispose();
	}

	internal class MineRefineryAssignment
	{
		public Actor? Mine { get; set; }

		public Actor? Refinery { get; set; }

		public List<Actor> CrateTransporters { get; set; } = [];

		public int ExpectedCrateTransporterCount { get; init; }

		public void AssignCrateTransporters(List<Actor> freeCrateTransporters)
		{
			if (this.CrateTransporters.Count >= this.ExpectedCrateTransporterCount || freeCrateTransporters.Count == 0)
				return;

			for (var i = this.CrateTransporters.Count; i <= this.ExpectedCrateTransporterCount; i++)
			{
				if (freeCrateTransporters.Count == 0)
					break;

				var transporter = freeCrateTransporters[^1];
				freeCrateTransporters.RemoveAt(freeCrateTransporters.Count - 1);

				this.CrateTransporters.Add(transporter);
			}
		}

		public void OrderCrateTransportersToWork(IBot bot, HashSet<Actor> availableCrateTransporters)
		{
			if (this.Mine == null || this.Refinery == null)
				return;

			// Process already assigned crate transporters
			foreach (var actor in this.CrateTransporters)
				ProcessCrateTransporter(actor);

			// Try assigning new crate transporter, if there's currently not enough of them
			for (var i = this.CrateTransporters.Count; i < this.ExpectedCrateTransporterCount; i++)
			{
				var crateTransporter = availableCrateTransporters.FirstOrDefault();
				if (crateTransporter == null)
					break; // no additional transporters available

				availableCrateTransporters.Remove(crateTransporter);
				this.CrateTransporters.Add(crateTransporter);

				ProcessCrateTransporter(crateTransporter);
			}

			void ProcessCrateTransporter(Actor actor)
			{
				if (!actor.TryGetTrait<CrateTransporter>(out var crateTransporter))
					return;

				if (!actor.TryGetTrait<CrateTransporterRoutine>(out var routine))
					return;

				// TODO: handle Mine depletion
				if ((crateTransporter.HasCrate && routine.CurrentRefinery != this.Refinery) || actor.IsIdle)
					QueueDockOrder(actor, this.Refinery, false, [this.Mine]);
				else if ((!crateTransporter.HasCrate && routine.CurrentMine != this.Mine) || actor.IsIdle)
					QueueDockOrder(actor, this.Mine, false, [this.Refinery]);
			}

			void QueueDockOrder(Actor actor, Actor? target, bool isQueued, Actor[]? extraActors = null)
			{
				var order = new Order(CrateTransporterRoutine.TransportCratesOrderID, actor, Target.FromActor(target), isQueued)
				{
					ExtraActors = extraActors ?? []
				};

				bot.QueueOrder(order);
			}
		}

		internal void RemoveInvalidCrateTransporters()
		{
			for (var i = this.CrateTransporters.Count - 1; i >= 0; i--)
			{
				var crateTransporter = this.CrateTransporters[i];
				if (crateTransporter.IsDead)
					this.CrateTransporters.RemoveAt(i);
			}
		}
	}
}
