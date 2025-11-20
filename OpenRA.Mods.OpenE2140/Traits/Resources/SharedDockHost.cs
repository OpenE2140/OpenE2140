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
using OpenRA.Mods.OpenE2140.Traits.Resources.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits;

[Desc("A version of dock that shares docking position with other SharedDockHosts.")]
public class SharedDockHostInfo : DockHostInfo, IDockHostInfo, Requires<ISharedDockHostManagerInfo>
{
	public override object Create(ActorInitializer init)
	{
		return new SharedDockHost(init.Self, this);
	}
}

public class SharedDockHost : DockHost
{
	public new readonly SharedDockHostInfo Info;

	private readonly ISharedDockHostManager manager;

	public SharedDockHost(Actor self, SharedDockHostInfo info)
		: base(self, info)
	{
		this.Info = info;
		this.manager = self.Trait<ISharedDockHostManager>();
	}

	public override void QueueDockActivity(Activity moveToDockActivity, Actor self, Actor clientActor, DockClientManager client)
	{
		moveToDockActivity.QueueChild(new DockHostLock(this,
			new GenericDockSequence(
				clientActor,
				client,
				self,
				this,
				this.Info.DockWait,
				this.Info.IsDragRequired,
				this.Info.DragOffset,
				this.Info.DragLength)));
	}

	public override bool IsDockingPossible(Actor clientActor, IDockClient client, bool ignoreReservations = false)
	{
		if (!this.manager.IsDockingPossible(this, clientActor, client, ignoreReservations))
			return false;

		return base.IsDockingPossible(clientActor, client, ignoreReservations);
	}

	internal bool TryAcquireLock(Actor clientActor)
	{
		return this.manager.TryAcquireLock(clientActor);
	}

	internal void ReleaseLock(Actor clientActor)
	{
		this.manager.TryReleaseLock(clientActor);
	}
}
