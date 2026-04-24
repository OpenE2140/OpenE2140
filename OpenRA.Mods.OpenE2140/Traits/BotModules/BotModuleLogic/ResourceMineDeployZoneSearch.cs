using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.BotModules.BotModuleLogic;

public class ResourceMineDeployZoneSearch
{
	private readonly Map map;
	private readonly IResourceLayer resourceLayer;
	private readonly int resourceCellClusterMinimumCount;
	private readonly int minimumResourceCellsToDeploy;

	public ResourceMineDeployZoneSearch(
		Map map,
		IResourceLayer resourceLayer,
		EconomyManagerBotModuleInfo economyManagerBotModuleInfo)
	{
		this.map = map;
		this.resourceLayer = resourceLayer;
		this.resourceCellClusterMinimumCount = economyManagerBotModuleInfo.ResourceCellClusterMinimumCount;
		this.minimumResourceCellsToDeploy = economyManagerBotModuleInfo.MinimumResourceCellsToDeploy;
	}

	public List<DeployZone> FindResourceMineDeployZones(
		CPos origin,
		int maxSearchRadius,
		CVec[] footprintOffsets)
	{
		// Phase 1: discover connected resource clusters (8-neighbour BFS) within search radius

		// Find clusters of resources, which contain at minimum clusterMin resource cells
		var clusters = this.FindResourceClusters(origin, maxSearchRadius, this.resourceCellClusterMinimumCount);

		if (clusters.Count == 0)
			return [];

		// Phase 2: for each cell in each cluster, consider anchors (top-left) that would place that cell inside the footprint.
		var seenAnchors = new HashSet<CPos>();
		var candidateZones = new List<DeployZone>();

		foreach (var cluster in clusters)
		{
			var deployZoneCells = new List<CPos>();

			// Each cluster has at least one cell
			var preferredClusterCenter = cluster[0];
			var maxResourceCellCount = int.MinValue;
			foreach (var cell in cluster)
			{
				foreach (var offset in footprintOffsets)
				{
					// Candidate top-left cell for the Refinery building
					var anchor = cell - offset;
					if (seenAnchors.Contains(anchor))
						continue;

					seenAnchors.Add(anchor);

					// Validate footprint is fully on-map
					var ok = true;
					foreach (var off2 in footprintOffsets)
					{
						if (!this.map.Contains(anchor + off2))
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
					if (resourceCount >= this.minimumResourceCellsToDeploy)
						deployZoneCells.Add(anchor);

					// Find preferred location within cluster
					if (resourceCount > maxResourceCellCount)
					{
						maxResourceCellCount = resourceCount;
						preferredClusterCenter = cell;
					}
				}
			}

			var deployZone = new DeployZone
			{
				CandidateCells = deployZoneCells,
				PreferredLocation = preferredClusterCenter
			};

			candidateZones.Add(deployZone);
		}

		return candidateZones;
	}

	private List<List<CPos>> FindResourceClusters(
		CPos origin,
		int maxSearchRadius,
		int minClusterSize,
		Func<CPos, bool>? shouldIgnoreCell = null)
	{
		var processed = new HashSet<CPos>();
		var clusters = new List<List<CPos>>();

		var maximumTileSearchRange = this.map.Grid.MaximumTileSearchRange;
		IEnumerable<CPos> searchTiles;
		if (maxSearchRadius <= maximumTileSearchRange)
			searchTiles = this.map.FindTilesInCircle(origin, maxSearchRadius);
		else
			searchTiles = FindTilesInAnnulusSpatialChunked(this.map, origin, 0, maxSearchRadius);

		foreach (var cell in searchTiles)
		{
			if (processed.Contains(cell) || shouldIgnoreCell?.Invoke(cell) == true)
				continue;

			if (!this.map.Contains(cell))
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

				foreach (var d in CVec.Directions)
				{
					var n = cur + d;
					if (processed.Contains(n))
						continue;

					if (!this.map.Contains(n))
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

			// If cluster large enough, include it for later anchor generation
			if (cluster.Count >= minClusterSize)
				clusters.Add(cluster);
		}

		return clusters;

		static IEnumerable<CPos> FindTilesInAnnulusSpatialChunked(Map map, CPos center, int minRange, int maxRange, bool allowOutsideBounds = false)
		{
			var maxChunkSize = map.Grid.MaximumTileSearchRange;
			var results = new HashSet<CPos>();

			var minRangeSquared = minRange * minRange;
			var maxRangeSquared = maxRange * maxRange;

			var minBounds = center + new CVec(-maxRange, -maxRange);
			var maxBounds = center + new CVec(maxRange, maxRange);

			for (var chunkX = minBounds.X; chunkX <= maxBounds.X; chunkX += maxChunkSize)
			{
				for (var chunkY = minBounds.Y; chunkY <= maxBounds.Y; chunkY += maxChunkSize)
				{
					var chunkCenter = new CPos(chunkX, chunkY);

					foreach (var tile in map.FindTilesInCircle(chunkCenter, maxChunkSize, allowOutsideBounds))
					{
						var offset = tile - center;
						var distanceSquared = offset.LengthSquared;

						if (distanceSquared >= minRangeSquared && distanceSquared <= maxRangeSquared)
							results.Add(tile);
					}
				}
			}

			return results;
		}
	}
}
