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

using OpenRA.Mods.Common.Pathfinder;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.BotModules.BotModuleLogic;

public record MineDeployLocation(CPos TopLeft, CPos CenterCell);

public class ResourceRefineryDeployZoneSearch
{
	private readonly int minRefineryDistance;
	private readonly int maxRefineryDistance;
	private readonly int maxGroundPathFactor;
	private readonly OpenRA.World world;

	public ResourceRefineryDeployZoneSearch(OpenRA.World world, EconomyManagerBotModuleInfo info)
	{
		this.world = world;
		this.minRefineryDistance = info.MinRefineryDistance;
		this.maxRefineryDistance = info.MaxRefineryDistance;
		this.maxGroundPathFactor = info.MaxRefineryGroundPathFactor;
	}

	public List<DeployZone> FindRefineryDeployZones(
		Locomotor? locomotor,
		ICustomBuildingInfo refineryBuildingInfo,
		CVec refineryOffset,
		List<MineDeployLocation> mineLocations)
	{
		var candidateZones = new List<DeployZone>();
		var checkedLocations = new HashSet<CPos>();

		foreach (var mine in mineLocations)
		{
			// If locomotor is available (mobile crate transporters), use cheap(-ish) algorithm for determining preferred deploy locations.
			// It rejects locations, where ground path distance from Mine's location is too high compared to crow distance.
			// This makes the bot avoid placing Refinery for example on other side of the cliff (which would force Banthas to travel long distance).
			// No locomotor: skip ground-path validation (e.g. aircraft transporter). In this case, simple check for max crow distance from Mine is sufficient.
			var groundDistances = locomotor != null ? this.ComputeGroundDistances(mine.CenterCell, locomotor) : null;

			var deployZoneCells = new List<CPos>();

			foreach (var cell in this.world.Map.FindTilesInAnnulus(mine.TopLeft, this.minRefineryDistance, this.maxRefineryDistance))
			{
				if (!checkedLocations.Add(cell) || !this.world.Map.Contains(cell))
					continue;

				if (!refineryBuildingInfo.IsCellBuildable(this.world, cell))
					continue;

				var refineryCenter = this.world.Map.CellContaining(refineryBuildingInfo.GetCenterOfFootprint(cell + refineryOffset));
				var crowDistance = (refineryCenter - mine.CenterCell).Length;
				var maxAllowedGroundDistance = crowDistance * this.maxGroundPathFactor;

				if (groundDistances != null
					&& (!groundDistances.TryGetValue(refineryCenter, out var groundDistance) || groundDistance > maxAllowedGroundDistance))
					continue;

				deployZoneCells.Add(cell);
			}

			var zone = new DeployZone
			{
				PreferredLocation = mine.TopLeft,
				CandidateCells = deployZoneCells
			};
			candidateZones.Add(zone);
		}

		return candidateZones;
	}

	private Dictionary<CPos, int> ComputeGroundDistances(CPos origin, Locomotor locomotor)
	{
		var distances = new Dictionary<CPos, int>
		{
			[origin] = 0
		};

		var queue = new Queue<CPos>();
		queue.Enqueue(origin);

		var maxDistance = this.maxRefineryDistance * this.maxGroundPathFactor;

		while (queue.Count > 0)
		{
			var current = queue.Dequeue();
			var currentDistance = distances[current];
			if (currentDistance >= maxDistance)
				continue;

			foreach (var direction in CVec.Directions)
			{
				var neighbor = current + direction;
				if (distances.ContainsKey(neighbor) || !this.world.Map.Contains(neighbor))
					continue;

				if (locomotor.MovementCostForCell(neighbor) == PathGraph.MovementCostForUnreachableCell)
					continue;

				distances[neighbor] = currentDistance + 1;
				queue.Enqueue(neighbor);
			}
		}

		return distances;
	}
}
