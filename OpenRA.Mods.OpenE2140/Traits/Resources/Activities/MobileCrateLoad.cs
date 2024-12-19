#region Copyright & License Information

/*
 * Copyright (c) The OpenE2140 Developers and Contributors
 * This file is part of OpenE2140, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */

# endregion

using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Activites;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Resources.Activities;

public class MobileCrateLoad : CrateLoadBase
{
	private readonly Mobile mobile;
	private readonly MobileCrateTransporter mobileCrateTransporter;
	private readonly MoveCooldownHelper moveCooldownHelper;

	public MobileCrateLoad(Actor self, in Target crateActor)
		: base(self, crateActor)
	{
		this.mobile = self.Trait<Mobile>();
		this.mobileCrateTransporter = self.Trait<MobileCrateTransporter>();

		this.moveCooldownHelper = new MoveCooldownHelper(self.World, this.mobile);
	}

	protected override void InitialMoveToCrate(Actor self, Target target)
	{
		this.moveCooldownHelper.NotifyMoveQueued();
		this.QueueChild(new MoveToCrate(self, target, target.CenterPosition, targetLineColor: Color.Green));
	}

	protected override void StartDocking(Actor self, Action continuationCallback)
	{
		this.QueueChild(new ResourceCrateMovementActivity(self, true, DockAnimation.Docking, this.mobileCrateTransporter.Info.LoadSequence, continuationCallback));
	}

	protected override void StartUndocking(Actor self, Action continuationCallback)
	{
		this.QueueChild(new ResourceCrateMovementActivity(self, true, DockAnimation.Undocking, this.mobileCrateTransporter.Info.LoadSequence, continuationCallback));
	}

	protected override bool CanLoadCrateNow(Actor self, Target target)
	{
		return target.Type == TargetType.Actor && (target.Actor.Location - self.Location).Length == 1;
	}

	protected override bool TryGetDockToDockPosition(Actor self, Target target, bool targetIsHiddenActor)
	{
		if (this.moveCooldownHelper.TryTick(targetIsHiddenActor, out var result))
			return result.Value;

		if (!this.CanLoadCrateNow(self, target))
		{
			this.moveCooldownHelper.NotifyMoveQueued();
			this.QueueChild(new MoveToCrate(self, target));

			return false;
		}

		var desiredFacing = (self.Location - target.Actor.Location).ToWVec().Yaw;

		if (this.mobile.Facing != desiredFacing)
		{
			this.QueueChild(new Turn(self, desiredFacing));
			return false;
		}

		return true;
	}

	protected override void StartDragging(Actor self, Target target)
	{
		var vec = target.Actor.Location - self.Location;
		var loadPosition = self.World.Map.CenterOfCell(self.Location) + CrateLoadUnloadHelpers.GetDockVector(vec);

		this.DragToPosition(self, loadPosition, self.Location);
	}

	protected override void StartUndragging(Actor self)
	{
		this.DragToPosition(self, self.World.Map.CenterOfCell(self.Location), self.Location);
	}

	private void DragToPosition(Actor self, WPos targetPosition, CPos cell)
	{
		this.TryQueueChild(CommonActivities.DragToPosition(self, this.mobile, targetPosition, cell, this.CrateTransporter.Info.DockSpeedModifier));
	}

	private class MoveToCrate : MoveAdjacentTo
	{
		private readonly IFacing crateFacing;

		public MoveToCrate(Actor self, in Target target, WPos? initialTargetPosition = null, Color? targetLineColor = null)
			: base(self, target, initialTargetPosition, targetLineColor)
		{
			this.crateFacing = target.Actor.Trait<IFacing>();
		}

		protected override (bool AlreadyAtDestination, List<CPos> Path) CalculatePathToTarget(Actor self, BlockedByActor check)
		{
			if (this.Target.Type == TargetType.Invalid)
				return (false, PathFinder.NoPath);

			// PERF: Assume that candidate cells don't change within a tick to avoid repeated queries
			// when Move enumerates different BlockedByActor values.
			if (this.searchCellsTick != self.World.WorldTick)
			{
				this.SearchCells.Clear();
				this.searchCellsTick = self.World.WorldTick;

				var wVec = new WVec(0, -2048, 0).Rotate(this.crateFacing.Orientation) / 1024;
				var cVec = new CVec(wVec.X, wVec.Y) / wVec.Length;

				var crateLocation = self.World.Map.CellContaining(this.Target.CenterPosition);
				var candidateCells = new[]
				{
					crateLocation + cVec,
					crateLocation - cVec
				};

				foreach (var cell in candidateCells)
				{
					if (this.Mobile.CanStayInCell(cell) && this.Mobile.CanEnterCell(cell, ignoreActor: self))
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
}
