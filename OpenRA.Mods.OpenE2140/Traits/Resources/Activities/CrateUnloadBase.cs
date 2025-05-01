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
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Resources.Activities;

public abstract class CrateUnloadBase : Activity
{
	private enum DockingState { MovingToUnload, Drag, Dock, Loop, Undock, Undrag, Complete }

	private readonly CrateTransporter crateTransporter;

	private CPos? targetLocation;
	private DockingState state;
	private bool dockInitiated;

	protected CrateUnloadBase(Actor self, CPos? targetLocation = null)
	{
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

		this.InitialMoveToCrate(self, this.targetLocation.Value);
		this.state = DockingState.MovingToUnload;
	}

	protected abstract void InitialMoveToCrate(Actor self, CPos targetLocation);

	public override bool Tick(Actor self)
	{
		if (this.targetLocation == null)
			return true;

		var targetLocation = this.targetLocation.Value;

		switch (this.state)
		{
			case DockingState.MovingToUnload:
			{
				if (this.IsCanceling)
					return true;

				if (!this.TryGetDockToDockPosition(self, targetLocation))
					return false;

				this.state = DockingState.Drag;

				return false;
			}
			case DockingState.Drag:
			{
				if (this.IsCanceling || !this.crateTransporter.CanUnloadAt(self, targetLocation))
					return true;

				this.StartDragging(self, targetLocation);

				this.state = DockingState.Dock;

				return false;
			}
			case DockingState.Dock:
			{
				if (!this.IsCanceling && this.CanUnloadCrateNow(self, targetLocation))
				{
					this.dockInitiated = true;
					this.StartDocking(self, () => this.state = DockingState.Loop);
				}
				else
				{
					this.crateTransporter.UnloadComplete();
					this.StartUndragging(self);
					this.state = DockingState.Complete;
				}

				return false;
			}
			case DockingState.Loop:
			{
				this.crateTransporter.UnloadCrate(targetLocation);

				this.state = DockingState.Undock;

				return false;
			}
			case DockingState.Undock:
			{
				if (this.dockInitiated)
				{
					this.StartUndocking(self, () => this.state = DockingState.Undrag);
				}
				else
				{
					this.state = DockingState.Complete;
				}

				return false;
			}
			case DockingState.Undrag:
			{
				this.StartUndragging(self);
				this.state = DockingState.Complete;

				return false;
			}
			case DockingState.Complete:
			{
				return true;
			}
		}

		throw new InvalidOperationException("Invalid crate transporter unload state");
	}

	protected abstract void StartUndragging(Actor self);

	protected abstract void StartDragging(Actor self, CPos targetLocation);

	protected abstract bool TryGetDockToDockPosition(Actor self, CPos targetLocation);

	protected abstract void StartUndocking(Actor self, Action continuationCallback);

	protected abstract void StartDocking(Actor self, Action continuationCallback);

	protected abstract bool CanUnloadCrateNow(Actor self, CPos targetLocation);

	public override IEnumerable<TargetLineNode> TargetLineNodes(Actor self)
	{
		if (this.targetLocation != null)
			yield return new TargetLineNode(Target.FromCell(self.World, this.targetLocation.Value), Color.Green);
	}
}
