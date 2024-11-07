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
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Resources.Activities;

public class CrateLoad : Activity
{
	private static readonly int NonDiagonalDockDistance = 405;
	private static readonly int DiagonalDockDistance = 570;

	private enum LoadState { MovingToLoad, Drag, Dock, Loop, Undock, Complete }

	private readonly Mobile mobile;
	private readonly CrateTransporter crateTransporter;
	private readonly Actor crateActor;
	private readonly MoveCooldownHelper moveCooldownHelper;
	private LoadState state;
	private bool dockInitiated;

	public CrateLoad(Actor self, Actor crateActor)
	{
		this.mobile = self.Trait<Mobile>();
		this.crateTransporter = self.Trait<CrateTransporter>();
		this.crateActor = crateActor;

		this.moveCooldownHelper = new MoveCooldownHelper(self.World, this.mobile);
	}

	protected override void OnFirstRun(Actor self)
	{
		// TODO: support frozen targets

		if (!this.crateTransporter.CanLoad(this.crateActor))
		{
			this.Cancel(self, true);
			return;
		}

		this.moveCooldownHelper.NotifyMoveQueued();
		this.QueueChild(new MoveToCrate(self, Target.FromActor(this.crateActor), targetLineColor: Color.Green));
		this.state = LoadState.MovingToLoad;
	}

	public override bool Tick(Actor self)
	{
		switch (this.state)
		{
			case LoadState.MovingToLoad:
			{
				if (this.IsCanceling || this.crateActor.Disposed || !this.crateActor.IsInWorld)
					return true;

				if (this.moveCooldownHelper.TryTick(false, out var result))
					return result.Value;

				if (!this.CanLoadCrateNow(self))
				{
					this.moveCooldownHelper.NotifyMoveQueued();
					this.QueueChild(new MoveToCrate(self, Target.FromActor(this.crateActor), targetLineColor: Color.Green));

					return false;
				}

				if (!this.TurnToLoadCrate(self))
					return false;

				this.state = LoadState.Drag;

				return false;
			}
			case LoadState.Drag:
			{
				if (this.IsCanceling || !this.CanLoadCrateNow(self) || !this.crateTransporter.CanLoad(this.crateActor))
					return true;

				var vec = this.crateActor.Location - self.Location;
				var isDiagonal = vec.X != 0 && vec.Y != 0;
				var unloadPosition = self.World.Map.CenterOfCell(self.Location)
					+ new WVec(vec.X, vec.Y, 0) * (isDiagonal ? DiagonalDockDistance : NonDiagonalDockDistance);

				this.DragToPosition(self, unloadPosition, self.Location);

				this.state = LoadState.Dock;

				return false;
			}
			case LoadState.Dock:
			{
				if (!this.IsCanceling && this.CanLoadCrateNow(self))
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
				this.crateTransporter.LoadCrate(self, this.crateActor);

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
				this.DragToPosition(self, self.World.Map.CenterOfCell(self.Location), self.Location);
				return true;
			}
		}

		throw new InvalidOperationException("Invalid crate transporter load state");
	}

	private bool CanLoadCrateNow(Actor self)
	{
		return (this.crateActor.Location - self.Location).Length == 1;
	}

	private bool TurnToLoadCrate(Actor self)
	{
		var desiredFacing = (self.Location - this.crateActor.Location).ToWVec().Yaw;

		if (this.mobile.Facing != desiredFacing)
		{
			this.QueueChild(new Turn(self, desiredFacing));
			return false;
		}

		return true;
	}

	// TODO: refactor
	private void DragToPosition(Actor self, WPos targetPosition, CPos cell)
	{
		var ticksToDock = (self.CenterPosition - targetPosition).Length / this.GetDockSpeed(cell);

		if (ticksToDock <= 0)
			return;

		this.QueueChild(new Drag(self, self.CenterPosition, targetPosition, ticksToDock));
	}

	private int GetDockSpeed(CPos cell)
	{
		var speedModifier = 30;
		return this.mobile.Locomotor.MovementSpeedForCell(cell) * speedModifier / 100;
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
