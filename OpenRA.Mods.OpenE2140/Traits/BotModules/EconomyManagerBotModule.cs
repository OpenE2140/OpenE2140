using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Extensions;
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

public class EconomyManagerBotModule : ConditionalTrait<EconomyManagerBotModuleInfo>, IBotTick, INotifyActorDisposing, IBotEconomyManager
{
	private readonly OpenRA.World world;
	private readonly Player player;
	private readonly ActorIndex.OwnerAndNamesAndTrait<CrateTransporterInfo> crateTransporters;
	private readonly ActorIndex.OwnerAndNamesAndTrait<ResourceMineInfo> mines;
	private readonly ActorIndex.OwnerAndNamesAndTrait<ResourceRefineryInfo> refineries;

	private IBotRequestUnitProduction[] requestUnitProduction = [];
	private IBotMcuBaseBuilder[] mcuBaseBuilder = [];
	private McuDeployManagerBotModule? mcuDeployManager;
	private IResourceLayer? resourceLayer;

	private int logicTicks;
	private bool hasSufficientEconomy;

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
		this.mcuDeployManager = this.player.PlayerActor.TraitOrDefault<McuDeployManagerBotModule>();
		this.resourceLayer = self.World.WorldActor.TraitOrDefault<IResourceLayer>();
	}

	protected override void TraitEnabled(Actor self)
	{
		// Avoid all AIs running their logic the same tick, randomize their initial scan delay.
		this.logicTicks = this.world.LocalRandom.Next(this.Info.LogicInterval);
	}

	void IBotTick.BotTick(IBot bot)
	{
		if (--this.logicTicks > 0)
			return;

		this.logicTicks = this.Info.LogicInterval;

		this.hasSufficientEconomy = this.Tick(bot);
	}

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
			var crateTransporterCount = this.crateTransporters.Alive().Count();
			var expectedCount = 2;
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
			if (mcu != null
				&& mcuBaseBuilder.RequestedProductionCount(bot, mcu.Name) == 0
				&& mcuBaseBuilder.InProductionCount(bot, mcu.Name) == 0
				&& (this.mcuDeployManager == null || this.mcuDeployManager.UndeployedMcuCount(bot, mcu.Name) == 0))
			{
				mcuBaseBuilder.RequestBuildingProduction(bot, mcu.Name);
				return true;
			}

			return false;
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

		// Find nearby mines (only those alive and within a reasonable search radius)
		var maxSearchRadiusSq = this.Info.MaxRefineryDistance.PowerOf2();
		var mineActors = this.mines.Alive()
			.Where(a =>
			{
				var mineBuildingInfo = CustomBuildingInfoWrapper.WrapIfNecessary(a.Info);
				if (mineBuildingInfo == null)
					return false;

				var mineCenterLocation = this.world.Map.CellContaining(mineBuildingInfo.GetCenterOfFootprint(a.Location));

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
}
