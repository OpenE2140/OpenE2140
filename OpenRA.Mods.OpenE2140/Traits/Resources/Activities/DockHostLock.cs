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

namespace OpenRA.Mods.OpenE2140.Traits.Resources.Activities;

public class DockHostLock : Activity
{
	private readonly Actor dockActor;
	private readonly SharedDockHost sharedDockHost;
	private readonly Activity dockActivity;
	private readonly bool releaseOnFinish;

	private bool hasDockStarted;
	private bool wasCanceled;

	public DockHostLock(Actor dockActor, SharedDockHost sharedDockHost, Activity dockActivity, bool releaseOnFinish = true)
	{
		this.dockActor = dockActor;
		this.sharedDockHost = sharedDockHost;
		this.dockActivity = dockActivity;
		this.releaseOnFinish = releaseOnFinish;
		this.ChildHasPriority = false;
	}

	public override bool Tick(Actor self)
	{
		if (this.IsCanceling)
		{
			// Let child activities properly finish (undock, drag out, etc.)
			if (!this.TickChild(self))
				return false;

			this.wasCanceled = true;

			return true;
		}

		if (!this.TickChild(self))
			return false;

		if (!this.hasDockStarted)
		{
			if (this.dockActor.IsDead)
			{
				this.Cancel(self, true);
				return true;
			}

			if (!this.sharedDockHost.TryAcquireLock(self))
			{
				this.QueueChild(new Wait(5));
				return false;
			}

			this.hasDockStarted = true;
			this.QueueChild(this.dockActivity);
			return false;
		}

		return true;
	}

	protected override void OnLastRun(Actor self)
	{
		if (this.hasDockStarted && (this.releaseOnFinish || this.wasCanceled))
			this.sharedDockHost.ReleaseLock(self);
	}

	protected override void OnActorDispose(Actor self)
	{
		// CrateTransporter was destroyed, immediately release the lock (regardless of whether it should be held or not)
		this.sharedDockHost.ReleaseLock(self);
	}
}
