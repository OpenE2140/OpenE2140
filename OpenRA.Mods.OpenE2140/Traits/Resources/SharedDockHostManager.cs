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

using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits;

public interface ISharedDockHostManagerInfo : ITraitInfoInterface { }

public interface ISharedDockHostManager
{
	bool IsDockingPossible(SharedDockHost sharedDockHost, Actor clientActor, IDockClient client, bool ignoreReservations = false);

	bool TryAcquireLock(Actor clientActor);

	void TryReleaseLock(Actor clientActor);
}

public class SharedDockHostManager<InfoType> : PausableConditionalTrait<InfoType>, ISharedDockHostManager
	where InfoType : PausableConditionalTraitInfo
{
	public Actor? CurrentClientActor { get; private set; }

	public SharedDockHostManager(InfoType info)
		: base(info)
	{
	}

	public virtual bool IsDockingPossible(SharedDockHost sharedDockHost, Actor clientActor, IDockClient client, bool ignoreReservations = false)
	{
		return true;
	}

	public bool TryAcquireLock(Actor clientActor)
	{
		if (this.CurrentClientActor == clientActor)
			return true;

		if (!this.TryAcquireLockInner(clientActor))
			return false;

		if (this.CurrentClientActor == null)
		{
			this.CurrentClientActor = clientActor;

			return true;
		}

		return false;
	}

	protected virtual bool TryAcquireLockInner(Actor clientActor)
	{
		return true;
	}

	public void TryReleaseLock(Actor clientActor)
	{
		if (this.CurrentClientActor == clientActor)
			this.CurrentClientActor = null;
	}
}
