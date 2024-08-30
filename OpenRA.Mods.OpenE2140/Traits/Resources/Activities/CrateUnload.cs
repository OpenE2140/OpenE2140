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
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Activites;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Resources.Activities;

public class CrateUnload : Activity
{
	private enum DockingState { MovingToUnload, Drag, Dock, Loop, Undock, Complete }

	private readonly Mobile mobile;
	private readonly CrateTransporter crateTransporter;

	private CPos? targetLocation;
	private DockingState dockingState;
	private CPos? unloadingCell;
	private bool dockInitiated;

	public CrateUnload(Actor self, CPos? targetLocation = null)
	{
		this.mobile = self.Trait<Mobile>();
		this.crateTransporter = self.Trait<CrateTransporter>();
		this.targetLocation = targetLocation;
	}

	protected override void OnFirstRun(Actor self)
	{
		if (!this.crateTransporter.CanUnload())
		{
			this.Cancel(self, true);
			return;
		}

		if (this.targetLocation == null)
			this.targetLocation = self.Location;

		this.QueueChild(this.mobile.MoveTo(this.targetLocation.Value));
		this.dockingState = DockingState.MovingToUnload;
	}

	public override bool Tick(Actor self)
	{
		if (this.targetLocation == null)
			return true;

		var targetLocation = this.targetLocation.Value;

		switch (this.dockingState)
		{
			case DockingState.MovingToUnload:
			{
				this.unloadingCell = this.PickUnloadingCell(self);
				if (this.unloadingCell == null)
					return true;

				self.NotifyBlocker(this.unloadingCell.Value);

				this.QueueChild(this.mobile.MoveTo(this.unloadingCell.Value));
				this.dockingState = DockingState.Drag;

				return false;
			}
			case DockingState.Drag:
			{
				if (this.unloadingCell == null || this.IsCanceling || !this.crateTransporter.CanUnloadAt(self, targetLocation))
					return true;

				this.crateTransporter.ReserveUnloadLocation(targetLocation);

				var vec = targetLocation - self.Location;
				var isDiagonal = vec.X != 0 && vec.Y != 0;
				var unloadPosition = self.World.Map.CenterOfCell(this.unloadingCell.Value) + CrateLoadUnloadHelpers.GetDockVector(vec);

				this.DragToPosition(self, unloadPosition, this.unloadingCell.Value);

				this.dockingState = DockingState.Dock;

				return false;
			}
			case DockingState.Dock:
			{
				if (!this.IsCanceling && this.unloadingCell != null)
				{
					this.dockInitiated = true;
					this.QueueChild(new ResourceCrateMovementActivity(self, false, DockAnimation.Docking, () => this.dockingState = DockingState.Loop));
				}
				else
				{
					this.crateTransporter.UnloadComplete();
					this.dockingState = DockingState.Undock;
				}

				return false;
			}
			case DockingState.Loop:
			{
				this.crateTransporter.UnloadCrate(targetLocation);

				this.dockingState = DockingState.Undock;

				return false;
			}
			case DockingState.Undock:
			{
				if (this.dockInitiated && this.unloadingCell != null)
				{
					this.QueueChild(new ResourceCrateMovementActivity(self, false, DockAnimation.Undocking, () => this.dockingState = DockingState.Complete));
				}
				else
				{
					this.dockingState = DockingState.Complete;
				}

				return false;
			}
			case DockingState.Complete:
			{
				this.DragToPosition(self, self.World.Map.CenterOfCell(self.Location), self.Location);
				return true;
			}
		}

		throw new InvalidOperationException("Invalid crate transporter unload state");
	}

	private void DragToPosition(Actor self, WPos targetPosition, CPos cell)
	{
		this.TryQueueChild(CommonActivities.DragToPosition(self, this.mobile, targetPosition, cell, this.crateTransporter.Info.DockSpeedModifier));
	}

	private CPos? PickUnloadingCell(Actor self)
	{
		return Util.ExpandFootprint(self.Location, true).Exclude(self.Location)
			.Where(c => this.mobile.CanStayInCell(c) && this.mobile.CanEnterCell(c, self, BlockedByActor.All))
			.OrderBy(c =>
			{
				// Order candidate cells by the angle to target cell (i.e. cell on which CrateTransporter currently is)
				// This will make CrateTransporter pick unloading cell that will take least amount of time to move onto.
				var turnAngle = (c - self.Location).ToWVec().Yaw;
				return new WAngle((turnAngle.Angle - self.Orientation.Yaw.Angle) * Util.GetTurnDirection(self.Orientation.Yaw, turnAngle)).Angle;
			})
			.Cast<CPos?>()
			.FirstOrDefault();
	}

	public override IEnumerable<TargetLineNode> TargetLineNodes(Actor self)
	{
		if (this.targetLocation != null)
			yield return new TargetLineNode(Target.FromCell(self.World, this.targetLocation.Value), Color.Green);
	}
}
