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

namespace OpenRA.Mods.OpenE2140.Traits.BotModules.BotModuleLogic;

/// <summary>
/// Scans the map for contiguous water bodies around a given origin and returns a deployable shore band for each.
/// Similar to <see cref="ResourceMineDeployZoneSearch"/>, but looks for Water terrain clusters.
/// </summary>
public class WaterBodyClusterSearch
{
	// How many cells inland from the water to include in the candidate band.
	// Width 2 covers patterns like water -> shore -> cliff -> clear.
	private const int ShoreBandWidth = 2;

	private readonly Map map;
	private readonly int minimumWaterBodySize;

	private readonly List<WaterBodyCluster> clusters = [];
	private CPos? lastOrigin;
	private int lastRadius;

	internal IReadOnlyCollection<WaterBodyCluster> Clusters => this.clusters;

	public WaterBodyClusterSearch(Map map, int minimumWaterBodySize)
	{
		this.map = map;
		this.minimumWaterBodySize = minimumWaterBodySize;
	}

	public void UpdateAvailableWaterBodies(CPos origin, int maxSearchRadius)
	{
		if (this.lastOrigin == origin && this.lastRadius == maxSearchRadius)
			return;

		this.clusters.Clear();
		this.clusters.AddRange(this.FindWaterBodies(origin, maxSearchRadius));
		this.lastOrigin = origin;
		this.lastRadius = maxSearchRadius;
	}

	public List<WaterBaseDeployZone> GetWaterBaseDeployZones(CPos origin, int maxSearchRadius)
	{
		this.UpdateAvailableWaterBodies(origin, maxSearchRadius);

		var zones = new List<WaterBaseDeployZone>(this.clusters.Count);
		foreach (var cluster in this.clusters)
		{
			var shoreCells = this.GetShoreBand(cluster.Cells, origin, maxSearchRadius);
			if (shoreCells.Count == 0)
				continue;

			var preferredLocation = shoreCells.MinBy(c => (c - origin).LengthSquared);
			zones.Add(new WaterBaseDeployZone
			{
				CandidateCells = shoreCells,
				PreferredLocation = preferredLocation,
				WaterCells = cluster.Cells
			});
		}

		return zones;
	}

	private List<CPos> GetShoreBand(List<CPos> waterCells, CPos origin, int maxSearchRadius)
	{
		var maxSearchRadiusSquared = maxSearchRadius * maxSearchRadius;
		var shoreCells = new HashSet<CPos>();

		foreach (var waterCell in waterCells)
		{
			for (var dx = -ShoreBandWidth; dx <= ShoreBandWidth; dx++)
			{
				for (var dy = -ShoreBandWidth; dy <= ShoreBandWidth; dy++)
				{
					var cell = waterCell + new CVec(dx, dy);
					if (!this.map.Contains(cell))
						continue;

					if (this.IsWater(cell) || this.IsShore(cell))
						continue;

					if ((cell - origin).LengthSquared > maxSearchRadiusSquared)
						continue;

					shoreCells.Add(cell);
				}
			}
		}

		return shoreCells.ToList();
	}

	private List<WaterBodyCluster> FindWaterBodies(CPos origin, int maxSearchRadius)
	{
		var processed = new HashSet<CPos>();
		var bodies = new List<WaterBodyCluster>();

		var maximumTileSearchRange = this.map.Grid.MaximumTileSearchRange;
		IEnumerable<CPos> searchTiles;
		if (maxSearchRadius <= maximumTileSearchRange)
			searchTiles = this.map.FindTilesInCircle(origin, maxSearchRadius);
		else
			searchTiles = MapSearchHelper.FindTilesInAnnulusSpatialChunked(this.map, origin, 0, maxSearchRadius);

		foreach (var cell in searchTiles)
		{
			if (processed.Contains(cell))
				continue;

			if (!this.map.Contains(cell))
			{
				processed.Add(cell);
				continue;
			}

			if (!this.IsWater(cell))
			{
				processed.Add(cell);
				continue;
			}

			var cluster = new List<CPos>();
			var queue = new Queue<CPos>();
			queue.Enqueue(cell);
			processed.Add(cell);

			while (queue.Count > 0)
			{
				var current = queue.Dequeue();
				cluster.Add(current);

				foreach (var direction in CVec.Directions)
				{
					var neighbor = current + direction;
					if (processed.Contains(neighbor))
						continue;

					if (!this.map.Contains(neighbor))
					{
						processed.Add(neighbor);
						continue;
					}

					if (!this.IsWater(neighbor))
					{
						processed.Add(neighbor);
						continue;
					}

					processed.Add(neighbor);
					queue.Enqueue(neighbor);
				}
			}

			if (cluster.Count >= this.minimumWaterBodySize)
				bodies.Add(new WaterBodyCluster(cluster));
		}

		return bodies;
	}

	private bool IsWater(CPos cell)
	{
		return this.map.GetTerrainInfo(cell).Type == "Water";
	}

	private bool IsShore(CPos cell)
	{
		return this.map.GetTerrainInfo(cell).Type == "Shore";
	}
}

public record WaterBodyCluster(List<CPos> Cells);
