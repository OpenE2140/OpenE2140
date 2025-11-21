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

using System.Diagnostics;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits;

public class MoveToBuildingEntrance : MoveAdjacentTo
{
	private readonly BuildingCrew targetBuildingCrew;

	public MoveToBuildingEntrance(Actor self, in Target target, WPos? initialTargetPosition = null, Color? targetLineColor = null)
		: base(self, target, initialTargetPosition, targetLineColor)
	{
		this.targetBuildingCrew = target.Actor.Trait<BuildingCrew>();
	}

	protected override (bool AlreadyAtDestination, List<CPos> Path) CalculatePathToTarget(Actor self, BlockedByActor check)
	{
		// PERF: Assume that candidate cells don't change within a tick to avoid repeated queries
		// when Move enumerates different BlockedByActor values.
		if (this.searchCellsTick != self.World.WorldTick)
		{
			this.SearchCells.Clear();
			this.searchCellsTick = self.World.WorldTick;

			Debug.Assert(this.Target.Type == TargetType.Actor, $"Target is not actor: {this.Target}");

			var entryCells = this.targetBuildingCrew.Entrances.Select(c => c.EntryCell);

			foreach (var cell in entryCells)
			{
				if (this.Mobile.CanStayInCell(cell) && this.Mobile.CanEnterCell(cell))
				{
					if (cell == self.Location)
						return (true, PathFinder.NoPath);

					this.SearchCells.Add(cell);
				}
			}
		}

		if (this.SearchCells.Count == 0)
			return (false, PathFinder.NoPath);

		return (false, this.Mobile.PathFinder.FindPathToTargetCells(self, self.Location, this.SearchCells, check));
	}
}
