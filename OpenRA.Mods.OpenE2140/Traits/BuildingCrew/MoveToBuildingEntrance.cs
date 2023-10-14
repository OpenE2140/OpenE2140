﻿using System.Diagnostics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.BuildingCrew;

public class MoveToBuildingEntrance : MoveAdjacentTo
{
	private readonly BuildingCrew targetBuildingCrew;

	public MoveToBuildingEntrance(Actor self, in Target target, WPos? initialTargetPosition = null, Color? targetLineColor = null)
		: base(self, target, initialTargetPosition, targetLineColor)
	{
		this.targetBuildingCrew = target.Actor.Trait<BuildingCrew>();
	}

	protected override List<CPos> CalculatePathToTarget(Actor self, BlockedByActor check)
	{
		// PERF: Assume that candidate cells don't change within a tick to avoid repeated queries
		// when Move enumerates different BlockedByActor values.
		if (this.searchCellsTick != self.World.WorldTick)
		{
			this.SearchCells.Clear();
			this.searchCellsTick = self.World.WorldTick;

			Debug.Assert(this.targetBuildingCrew.Info.EntryCells.Length != 0, $"Building doesn't have any entry cells for BuildingCrew: {self}");
			Debug.Assert(this.Target.Type == TargetType.Actor, $"Target is not actor: {this.Target}");

			var entryCells = this.targetBuildingCrew.Info.EntryCells.Length > 0
				? this.targetBuildingCrew.Info.EntryCells.Select(c => this.Target.Actor.Location + c)
				: Util.AdjacentCells(self.World, this.Target);

			foreach (var cell in entryCells)
				if (this.Mobile.CanStayInCell(cell) && this.Mobile.CanEnterCell(cell))
					this.SearchCells.Add(cell);
		}

		if (this.SearchCells.Count == 0)
			return PathFinder.NoPath;

		return this.Mobile.PathFinder.FindPathToTargetCells(self, self.Location, this.SearchCells, check);
	}
}
