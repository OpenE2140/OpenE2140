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
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Resources.Activities;

public abstract class CrateLoadBase : Activity
{
	private enum LoadState { MovingToLoad, Drag, Dragging, Dock, Loop, Undock, Undrag, Complete }

	protected readonly CrateTransporter CrateTransporter;
	protected readonly ResourceCrate ResourceCrate;

	private Target target;
	private Target lastVisibleTarget;
	private bool useLastVisibleTarget;
	private LoadState state;
	private bool dockInitiated;

	protected CrateLoadBase(Actor self, in Target crateActor)
	{
		this.target = crateActor;

		this.CrateTransporter = self.Trait<CrateTransporter>();
		this.ResourceCrate = crateActor.Actor.Trait<ResourceCrate>();

		this.ChildHasPriority = false;
	}

	protected override void OnFirstRun(Actor self)
	{
		if (this.target.Type == TargetType.Invalid)
		{
			this.Cancel(self, true);
			return;
		}

		this.InitialMoveToCrate(self, this.target);
		this.state = LoadState.MovingToLoad;
	}

	protected abstract void InitialMoveToCrate(Actor self, Target target);

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

				// We are next to where we thought the target should be, but it isn't here
				// There's not much more we can do here
				if (this.useLastVisibleTarget || this.target.Type != TargetType.Actor)
					return true;

				if (this.IsCanceling)
					return true;

				if (!this.TryGetDockToDockPosition(self, this.target, targetIsHiddenActor))
					return false;

				if (!this.ResourceCrate.ReserveTransporter(self))
				{
					this.Cancel(self, true);
					return true;
				}

				this.state = LoadState.Drag;

				return false;
			}
			case LoadState.Drag:
			{
				if (this.IsCanceling || !this.CanLoadCrateNow(self, this.target) || !this.CrateTransporter.CanLoad(this.target.Actor))
				{
					this.ResourceCrate.UnreserveTransporter();
					return true;
				}

				this.StartDragging(self, this.target);

				this.state = LoadState.Dragging;

				return false;
			}
			case LoadState.Dragging:
			{
				if (this.TickChild(self))
				{
					this.state = LoadState.Dock;

					this.ChildHasPriority = true;
				}
				else
				{
					this.OnDragging(self);
				}

				return false;
			}
			case LoadState.Dock:
			{
				if (!this.IsCanceling && this.CanLoadCrateNow(self, this.target) && !this.target.Actor.Disposed)
				{
					this.dockInitiated = true;
					this.StartDocking(self, () => this.state = this.state = LoadState.Loop);

					return false;
				}

				this.state = LoadState.Undrag;
				goto case LoadState.Undrag;
			}
			case LoadState.Loop:
			{
				if (!this.target.Actor.Disposed)
					this.CrateTransporter.LoadCrate(self, this.target.Actor);

				this.state = LoadState.Undock;

				return false;
			}
			case LoadState.Undock:
			{
				if (this.dockInitiated)
				{
					this.StartUndocking(self, () => this.state = LoadState.Undrag);
				}
				else
				{
					this.state = LoadState.Undrag;
				}

				return false;
			}
			case LoadState.Undrag:
			{
				this.ResourceCrate.UnreserveTransporter();
				this.StartUndragging(self);
				this.state = LoadState.Complete;

				return false;
			}
			case LoadState.Complete:
			{
				return true;
			}
		}

		throw new InvalidOperationException("Invalid crate transporter load state");
	}

	protected abstract void StartUndragging(Actor self);

	protected abstract void StartDragging(Actor self, Target target);

	protected virtual void OnDragging(Actor self) { }

	protected abstract bool TryGetDockToDockPosition(Actor self, Target target, bool targetIsHiddenActor);

	protected abstract void StartUndocking(Actor self, Action continuationCallback);

	protected abstract void StartDocking(Actor self, Action continuationCallback);

	protected abstract bool CanLoadCrateNow(Actor self, Target target);

	public override IEnumerable<TargetLineNode> TargetLineNodes(Actor self)
	{
		yield return new TargetLineNode(this.useLastVisibleTarget ? this.lastVisibleTarget : this.target, Color.Green);
	}
}
