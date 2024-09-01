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

using OpenRA.Activities;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Activites;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Resources.Activities;

public class CrateLoad : Activity
{
	private enum LoadState { MovingToLoad, Drag, Dock, Loop, Undock, Complete }

	private readonly Mobile mobile;
	private readonly CrateTransporter crateTransporter;
	private readonly ResourceCrate resourceCrate;
	private readonly MoveCooldownHelper moveCooldownHelper;

	private Target target;
	private Target lastVisibleTarget;
	private bool useLastVisibleTarget;
	private LoadState state;
	private bool dockInitiated;

	public CrateLoad(Actor self, in Target crateActor)
	{
		this.mobile = self.Trait<Mobile>();
		this.crateTransporter = self.Trait<CrateTransporter>();
		this.target = crateActor;
		this.resourceCrate = crateActor.Actor.Trait<ResourceCrate>();

		this.ChildHasPriority = false;
		this.moveCooldownHelper = new MoveCooldownHelper(self.World, this.mobile);
	}

	protected override void OnFirstRun(Actor self)
	{
		if (this.target.Type == TargetType.Invalid)
		{
			this.Cancel(self, true);
			return;
		}

		this.moveCooldownHelper.NotifyMoveQueued();
		this.QueueChild(new MoveToCrate(self, this.target, this.target.CenterPosition, targetLineColor: Color.Green));
		this.state = LoadState.MovingToLoad;
	}

	public override bool Tick(Actor self)
	{
		switch (this.state)
		{
			case LoadState.MovingToLoad:
			{
				// Update our view of the target
				this.target = this.target.Recalculate(self.Owner, out var targetIsHiddenActor);
				if (!targetIsHiddenActor && this.target.Type == TargetType.Actor)
					this.lastVisibleTarget = Target.FromTargetPositions(this.target);

				this.useLastVisibleTarget = targetIsHiddenActor || !this.target.IsValidFor(self);

				// Cancel immediately if the target died while we were entering it
				if (!this.IsCanceling && this.useLastVisibleTarget && this.state == LoadState.Dock)
					this.Cancel(self, true);

				// We need to wait for movement to finish before transitioning to
				// the next state or next activity
				if (!this.TickChild(self))
					return false;

				var result = this.moveCooldownHelper.Tick(targetIsHiddenActor);
				if (result != null)
					return result.Value;

				// We are next to where we thought the target should be, but it isn't here
				// There's not much more we can do here
				if (this.useLastVisibleTarget || this.target.Type != TargetType.Actor)
					return true;

				if (!this.CanLoadCrateNow(self))
				{
					this.moveCooldownHelper.NotifyMoveQueued();
					this.QueueChild(new MoveToCrate(self, this.target));

					return false;
				}

				if (this.useLastVisibleTarget || this.target.Type != TargetType.Actor)
					return true;

				if (!this.TurnToLoadCrate(self))
					return false;

				if (!this.resourceCrate.ReserveTransporter(self))
				{
					this.Cancel(self, true);
					return true;
				}

				this.state = LoadState.Drag;
				this.ChildHasPriority = true;

				return false;
			}
			case LoadState.Drag:
			{
				if (this.IsCanceling || !this.CanLoadCrateNow(self) || !this.crateTransporter.CanLoad(this.target.Actor))
				{
					this.resourceCrate.UnreserveTransporter();
					return true;
				}

				var vec = this.target.Actor.Location - self.Location;
				var isDiagonal = vec.X != 0 && vec.Y != 0;
				var loadPosition = self.World.Map.CenterOfCell(self.Location) + CrateLoadUnloadHelpers.GetDockVector(vec);

				this.DragToPosition(self, loadPosition, self.Location);

				this.state = LoadState.Dock;

				return false;
			}
			case LoadState.Dock:
			{
				if (!this.IsCanceling && this.CanLoadCrateNow(self) && !this.target.Actor.Disposed)
				{
					this.dockInitiated = true;
					this.QueueChild(new ResourceCrateMovementActivity(self, true, DockAnimation.Docking, () => this.state = this.state = LoadState.Loop));
				}
				else
				{
					this.state = LoadState.Undock;
				}

				return false;
			}
			case LoadState.Loop:
			{
				if (!this.target.Actor.Disposed)
					this.crateTransporter.LoadCrate(self, this.target.Actor);

				this.state = LoadState.Undock;

				return false;
			}
			case LoadState.Undock:
			{
				if (this.dockInitiated)
				{
					this.QueueChild(new ResourceCrateMovementActivity(self, true, DockAnimation.Undocking, () => this.state = LoadState.Complete));
				}
				else
				{
					this.state = LoadState.Complete;
				}

				return false;
			}
			case LoadState.Complete:
			{
				this.resourceCrate.UnreserveTransporter();
				this.DragToPosition(self, self.World.Map.CenterOfCell(self.Location), self.Location);
				return true;
			}
		}

		throw new InvalidOperationException("Invalid crate transporter load state");
	}

	private bool CanLoadCrateNow(Actor self)
	{
		return this.target.Type == TargetType.Actor && (this.target.Actor.Location - self.Location).Length == 1;
	}

	private bool TurnToLoadCrate(Actor self)
	{
		var desiredFacing = (self.Location - this.target.Actor.Location).ToWVec().Yaw;

		if (this.mobile.Facing != desiredFacing)
		{
			this.QueueChild(new Turn(self, desiredFacing));
			return false;
		}

		return true;
	}

	private void DragToPosition(Actor self, WPos targetPosition, CPos cell)
	{
		this.TryQueueChild(CommonActivities.DragToPosition(self, this.mobile, targetPosition, cell, this.crateTransporter.Info.DockSpeedModifier));
	}

	public override IEnumerable<TargetLineNode> TargetLineNodes(Actor self)
	{
		yield return new TargetLineNode(this.useLastVisibleTarget ? this.lastVisibleTarget : this.target, Color.Green);
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
