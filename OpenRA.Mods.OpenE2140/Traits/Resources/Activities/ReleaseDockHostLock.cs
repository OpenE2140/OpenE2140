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

namespace OpenRA.Mods.OpenE2140.Traits.Resources.Activities;

/// <summary>
/// Releases lock on <see cref="SharedDockHost"/> when the child activity finishes.
/// </summary>
public class ReleaseDockHostLock : Activity
{
	private readonly SharedDockHost sharedDockHost;
	private readonly Activity childActivity;

	public ReleaseDockHostLock(SharedDockHost sharedDockHost, Activity childActivity)
	{
		this.sharedDockHost = sharedDockHost;
		this.childActivity = childActivity;
	}

	protected override void OnFirstRun(Actor self)
	{
		this.QueueChild(this.childActivity);
	}

	protected override void OnLastRun(Actor self)
	{
		this.sharedDockHost.ReleaseLock(self);
	}

	protected override void OnActorDispose(Actor self)
	{
		this.OnLastRun(self);
	}
}
