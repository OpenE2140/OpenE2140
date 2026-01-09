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

	[Desc("Delays in ticks between each time the AI should expand their economy.")]
	public readonly List<int> EconomyExpansionDelays = [];

	[Desc("Interval (in ticks) between performing the module logic.")]
	public readonly int LogicInterval = 50;

	[Desc("Minimum number of resource cells, which the Mine building's footprint must cover for acceptable MCU deployment.")]
	public readonly int MinimumResourceCellsToDeploy = 3;

	[Desc("Minimum number of adjacent resource cells that are required to consider this cluster, when deciding Mine location deployment.")]
	public readonly int ResourceCellClusterMinimumCount = 3;

	[Desc("Radius around Mine MCU location, where possible locations for Mine are checked, when deciding Mine location deployment.")]
	public readonly int MaxResourceCellsToCheck = 10;

	[Desc("Minimum distance from Mine, where Refinery can be placed.")]
	public readonly int MinRefineryDistance = 5;

	[Desc("Maximum distance from Mine, where Refinery can be placed.")]
	public readonly int MaxRefineryDistance = 12;

	[Desc("Number of crate transporters assigned to single Mine/Refinery pair.",
		"Be aware of the fact, that over the course of the game, AI can have more crate transporters: " +
		"for example Refinery gets destroyed, then rebuilt -> AI gets one free crate transporter from the Refinery.")]
	public readonly int CrateTransporterPerRefineryMinePair = 2;

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
	}
}

public class EconomyManagerBotModule : ConditionalTrait<EconomyManagerBotModuleInfo>, IBotTick, INotifyActorDisposing, IBotEconomyManager, IBotRequestPauseUnitProduction
{
	private readonly OpenRA.World world;
	private readonly Player player;
	private readonly ActorIndex.OwnerAndNamesAndTrait<CrateTransporterInfo> crateTransporters;
	private readonly ActorIndex.OwnerAndNamesAndTrait<ResourceMineInfo> mines;
	private readonly ActorIndex.OwnerAndNamesAndTrait<ResourceRefineryInfo> refineries;
	private readonly List<MineRefineryAssignment> mineRefineryAssignments = [];
	private readonly (List<Actor> Mines, List<Actor> Refineries) assignmentActorsDirtyCheck = ([], []);

	private IBotRequestUnitProduction[] requestUnitProduction = [];
	private IBotMcuBaseBuilder[] mcuBaseBuilder = [];
	private IBotMcuDeployManager[] mcuDeployManager = [];
	private IResourceLayer? resourceLayer;
	private PlayerIncomeTracker? incomeTracker;

	private IBotMcuDeployManager? McuDeployManager => this.mcuDeployManager.FirstEnabledTraitOrDefault();

	private int logicTicks;
	private bool hasSufficientEconomy;
	private int? expandingEconomySince;

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

		this.UpdateMineRefineryAssignments();

		this.OrderCrateTransporterToWork(bot);

		var hadSufficientEconomy = this.hasSufficientEconomy;
		this.hasSufficientEconomy = this.Tick(bot);

		if (this.hasSufficientEconomy)
			this.expandingEconomySince = null;
		else
			this.expandingEconomySince ??= this.world.WorldTick;

		if (!hadSufficientEconomy && this.hasSufficientEconomy)
		{
			AIUtils.BotDebug("{0} has sufficient economy", bot.Player);

			// Force reassigning mines/refineries, in case crate transporters got out of sync.
			this.assignmentActorsDirtyCheck.Mines.Clear();
			this.assignmentActorsDirtyCheck.Refineries.Clear();
		}
		else if (hadSufficientEconomy && !this.hasSufficientEconomy)
			AIUtils.BotDebug("{0} does NOT have sufficient economy and is expanding it", bot.Player);
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
				var searchResult = FindNearestActor(unassignedRefineries, mine.Location, (maxSearchRadius * i).PowerOf2());
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

	bool IBotRequestPauseUnitProduction.PauseUnitProduction => this.expandingEconomySince != null && (this.world.WorldTick - this.expandingEconomySince) > 100;

	private bool Tick(IBot bot)
	{
		var mcuBaseBuilder = this.mcuBaseBuilder.FirstEnabledTraitOrDefault();

		var mineCount = this.mines.Alive().Count();
		var refineryCount = this.refineries.Alive().Count();

		var currentEconomyLevel = Math.Min(mineCount, refineryCount);
		var targetEconomyLevel = this.Info.EconomyExpansionDelays.Count(t => t < this.world.WorldTick) + 1;

		var isEconomySufficient = currentEconomyLevel >= targetEconomyLevel;

		// Not enough mines -> build more
		var sufficientMineCount = mineCount >= targetEconomyLevel;
		if (!sufficientMineCount)
		{
			if (mcuBaseBuilder != null)
			{
				var mineMcu = this.world.GetMcuFromActor(this.Info.MineTypes.Random(this.world.LocalRandom));
				if (TryRequestProduction(bot, mcuBaseBuilder, mineMcu))
					return isEconomySufficient;
			}
		}

		// Not enough refineries -> build more
		var sufficientRefineryCount = refineryCount >= targetEconomyLevel;
		if (!sufficientRefineryCount)
		{
			if (mcuBaseBuilder != null)
			{
				var refinery = this.world.GetMcuFromActor(this.Info.RefineryTypes.Random(this.world.LocalRandom));
				if (TryRequestProduction(bot, mcuBaseBuilder, refinery))
					return isEconomySufficient;
			}
		}

		// Not enough crate transporters -> build more
		var unitBuilder = this.requestUnitProduction.FirstEnabledTraitOrDefault();
		if (unitBuilder != null && sufficientMineCount && sufficientRefineryCount)
		{
			// Take into account Refineries currently built or their deployment is in progress
			// (because Refinery provides one free crate transporter)
			var refineriesCurrentlyBuilt = mcuBaseBuilder != null ?
				GetProductionInProgressCount(bot, mcuBaseBuilder, this.Info.RefineryTypes.Select(this.world.GetMcuFromActor)) : 0;

			var crateTransporterCount = this.crateTransporters.Alive().Count() + refineriesCurrentlyBuilt;
			var expectedCount = targetEconomyLevel * this.Info.CrateTransporterPerRefineryMinePair;
			var enoughTransporters = crateTransporterCount >= expectedCount;

			var shouldBuild = !enoughTransporters
				&& this.mines.Alive().Any()
				&& this.refineries.Alive().Any();
			if (shouldBuild)
			{
				// Build the best crate transporter that's possible to build currently.
				var crateTransporterType = this.Info.CrateTransporterTypes
					.LastOrDefault(t => unitBuilder.CanBuildUnit(this.player, t));

				if (crateTransporterType != null
					&& unitBuilder.RequestedProductionCount(bot, crateTransporterType) == 0
					&& unitBuilder.InProductionCount(this.player, crateTransporterType) == 0)
					unitBuilder.RequestUnitProduction(bot, crateTransporterType);

				isEconomySufficient = true;
			}
			else if (!enoughTransporters)
				isEconomySufficient = false;
		}

		// TODO: other checks

		return isEconomySufficient;

		bool TryRequestProduction(IBot bot, IBotMcuBaseBuilder mcuBaseBuilder, ActorInfo? mcu)
		{
			if (mcu == null || GetProductionInProgressCount(bot, mcuBaseBuilder, [mcu]) > 0)
				return false;

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

	public List<CPos> GetDeployCellsCandidates(Actor mcu, CPos? target)
	{
		if (!McuUtils.TryGetTargetBuilding(this.world, mcu.Info, out var building))
			return [];

		// Both Mine and Refinery have different placement requirements
		if (this.Info.MineTypes.Contains(building.Name))
		{
			return this.FindResourceClusters(mcu, building, target);
		}
		else if (this.Info.RefineryTypes.Contains(building.Name))
		{
			return this.FindRefineryCandidateCells(mcu, building, target);
		}

		return [];
	}

	private List<CPos> FindRefineryCandidateCells(Actor mcu, ActorInfo building, CPos? target)
	{
		if (!building.TryGetTrait<BuildingInfo>(out var buildingInfo))
			return [];

		if (!mcu.Info.TryGetTrait<TransformsInfo>(out var transformsInfo))
			return [];

		// Origin = where the building's top-left would be if MCU stayed at its current location.
		var origin = mcu.Location + transformsInfo.Offset;

		// Find nearby Mines, which are:
		// - alive
		// - within a reasonable search radius
		// - and have currently unassigned Mine
		var maxSearchRadiusSq = this.Info.MaxRefineryDistance.PowerOf2();
		var mineActors = this.mines.Alive()
			.Where(mine =>
			{
				if (this.mineRefineryAssignments.Any(a => a.Mine == mine && a.Refinery != null))
					return false;

				var mineBuildingInfo = CustomBuildingInfoWrapper.WrapIfNecessary(mine.Info);
				if (mineBuildingInfo == null)
					return false;

				var mineCenterLocation = this.world.Map.CellContaining(mineBuildingInfo.GetCenterOfFootprint(mine.Location));

				return (mineCenterLocation - origin).LengthSquared <= maxSearchRadiusSq;
			})
			.ToArray();

		var checkedLocations = new HashSet<CPos>();
		var result = new List<CPos>();
		var candidatesByDistance = new SortedDictionary<int, List<CPos>>();

		// Evaluate each mine for suitable location, where new Refinery can be placed
		foreach (var mine in mineActors)
		{
			var mineMcuInfo = McuUtils.GetMcuActor(this.world, mine.Info);
			if (mineMcuInfo == null || !mineMcuInfo.TryGetTrait<TransformsInfo>(out var mineTransformsInfo))
				continue;

			var mineMcuDeployLocation = mine.Location - mineTransformsInfo.Offset;

			foreach (var cell in this.world.Map.FindTilesInAnnulus(mineMcuDeployLocation, this.Info.MinRefineryDistance, this.Info.MaxRefineryDistance))
			{
				if (checkedLocations.Contains(cell))
					continue;

				checkedLocations.Add(cell);

				if (target != null && (cell - target.Value).LengthSquared <= 3 * 3)
					continue;

				var distToMine = (cell - mineMcuDeployLocation).LengthSquared;

				if (!candidatesByDistance.TryGetValue(distToMine, out var cells))
					candidatesByDistance[distToMine] = cells = [];

				cells.Add(cell);
			}

			// Pick at least N candidates, which are closest to currently evaluated Mine
			var added = 0;
			foreach (var (dist, cells) in candidatesByDistance)
			{
				if (added > 30)
					break;

				result.AddRange(cells);
				added += cells.Count;
			}

			candidatesByDistance.Clear();
		}

		return result;
	}

	private List<CPos> FindResourceClusters(Actor mcu, ActorInfo building, CPos? target)
	{
		if (this.resourceLayer == null)
			return [];

		var clusterMin = this.Info.ResourceCellClusterMinimumCount;                // cluster must have at least this many resource cells
		var minFootprintResourceCells = this.Info.MinimumResourceCellsToDeploy; // building footprint must cover at least this many resource cells

		var map = this.world.Map;
		var origin = mcu.Location;

		if (!building.TryGetTrait<BuildingInfo>(out var buildingInfo))
			return [];

		if (!mcu.Info.TryGetTrait<TransformsInfo>(out var transformsInfo))
			return [];

		// Phase 1: discover connected resource clusters (8-neighbour BFS) within search radius
		var processed = new HashSet<CPos>();
		var goodClusterCells = new HashSet<CPos>(); // resource cells that belong to clusters >= clusterMin
		var directions = CVec.Directions;

		foreach (var cell in map.FindTilesInAnnulus(origin, 0, this.Info.MaxResourceCellsToCheck))
		{
			if (processed.Contains(cell))
				continue;

			if (!map.Contains(cell))
			{
				processed.Add(cell);
				continue;
			}

			// Non-resource -> mark visited and skip
			if (this.resourceLayer.GetResource(cell).Type == null)
			{
				processed.Add(cell);
				continue;
			}

			// BFS to collect this cluster
			var cluster = new List<CPos>();
			var q = new Queue<CPos>();
			q.Enqueue(cell);
			processed.Add(cell);

			while (q.Count > 0)
			{
				var cur = q.Dequeue();
				cluster.Add(cur);

				foreach (var d in directions)
				{
					var n = cur + d;
					if (processed.Contains(n))
						continue;

					if (!map.Contains(n))
					{
						processed.Add(n);
						continue;
					}

					if (this.resourceLayer.GetResource(n).Type == null)
					{
						processed.Add(n);
						continue;
					}

					processed.Add(n);
					q.Enqueue(n);
				}
			}

			// If cluster large enough, include its cells for later anchor generation
			if (cluster.Count >= clusterMin)
				foreach (var rc in cluster)
					goodClusterCells.Add(rc);
		}

		if (goodClusterCells.Count == 0)
			return [];

		// Phase 2: for each good cluster cell, consider anchors (top-left) that would place that cell inside the footprint.
		var footprintOffsets = buildingInfo.Footprint.Keys.Select(v => v + transformsInfo.Offset).ToArray();
		var seenAnchors = new HashSet<CPos>();
		var candidatesByDistance = new SortedDictionary<int, List<CPos>>();

		foreach (var cell in goodClusterCells)
		{
			foreach (var offset in footprintOffsets)
			{
				var anchor = cell - offset; // candidate top-left for the building
				if (seenAnchors.Contains(anchor))
					continue;

				seenAnchors.Add(anchor);

				if (target != null && (anchor - target.Value).LengthSquared <= 3 * 3)
					continue;

				// Validate footprint is fully on-map
				var ok = true;
				foreach (var off2 in footprintOffsets)
				{
					if (!map.Contains(anchor + off2))
					{
						ok = false;
						break;
					}
				}
				if (!ok)
					continue;

				// Count resource cells under the footprint...
				var resourceCount = 0;
				foreach (var off2 in footprintOffsets)
				{
					if (this.resourceLayer.GetResource(anchor + off2).Type != null)
						resourceCount++;
				}

				// ... and pick only those anchors, which have enough resource cells around them
				if (resourceCount >= minFootprintResourceCells)
				{
					var distToTarget = 0;
					if (target != null)
					{
						distToTarget = (anchor - target.Value).LengthSquared;
					}

					if (!candidatesByDistance.TryGetValue(distToTarget, out var cells))
						candidatesByDistance[distToTarget] = cells = [];

					cells.Add(anchor);
				}
			}
		}

		var result = new List<CPos>();

		foreach (var (dist, cells) in candidatesByDistance)
		{
			if (result.Count > 50)
				break;

			result.AddRange(cells);
		}

		return result;
	}

	void INotifyActorDisposing.Disposing(Actor self)
	{
		this.crateTransporters.Dispose();
		this.refineries.Dispose();
		this.mines.Dispose();
	}

	private class MineRefineryAssignment
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
				if (crateTransporter.HasCrate && routine.CurrentRefinery != this.Refinery || actor.IsIdle)
					QueueDockOrder(actor, this.Refinery, false, [this.Mine]);
				else if (!crateTransporter.HasCrate && routine.CurrentMine != this.Mine || actor.IsIdle)
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
