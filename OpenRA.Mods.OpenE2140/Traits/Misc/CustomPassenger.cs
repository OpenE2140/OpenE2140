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

namespace OpenRA.Mods.OpenE2140.Traits;

[Desc($"This actor can enter Cargo actors. Custom version of {nameof(Passenger)} trait, which can optionally preserve passenger's activities on unload.")]
public class CustomPassengerInfo : PassengerInfo
{
	[Desc("When unloading, cancel all other activies.")]
	public readonly bool CancelActivitiesOnExit = true;

	public override object Create(ActorInitializer init) { return new CustomPassenger(this); }
}

public class CustomPassenger : Passenger
{
	public new CustomPassengerInfo Info { get; }

	public CustomPassenger(CustomPassengerInfo info)
		: base(info)
	{
		this.Info = info;
	}

	public override void OnBeforeAddedToWorld(Actor actor)
	{
		if (this.Info.CancelActivitiesOnExit)
			actor.CancelActivity();
	}

	public override void OnEjectedFromKilledCargo(Actor self)
	{
		// It's possible that the passenger had some activities queued up before entering the Cargo.
		// If so, we need to queue the Nudge activity as child of that activity

		self.CurrentActivity.QueueChild(new Nudge(self));
	}
}
