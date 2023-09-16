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

using JetBrains.Annotations;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

[UsedImplicitly]
[Desc("This version of the conveyor belt ejects resource crates.")]
public class EjectingConveyorBeltInfo : ConveyorBeltInfo
{
	public override object Create(ActorInitializer init)
	{
		return new EjectingConveyorBelt(this);
	}
}

public class EjectingConveyorBelt : ConveyorBelt, INotifyAddedToWorld, INotifyOwnerChanged
{
	// TODO temporary => must be in refinery!
	private PlayerResources? playerResources;

	public EjectingConveyorBelt(ConveyorBeltInfo info)
		: base(info)
	{
	}

	protected override bool Complete(ResourceCrate crate)
	{
		// TODO Wait for crate pickup here.
		this.playerResources?.GiveCash(crate.Resources);

		return true;
	}

	// TODO temporary => must be in refinery!
	void INotifyAddedToWorld.AddedToWorld(Actor self)
	{
		this.playerResources = self.Owner.PlayerActor.TraitOrDefault<PlayerResources>();
	}

	// TODO temporary => must be in refinery!
	void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
	{
		this.playerResources = newOwner.PlayerActor.TraitOrDefault<PlayerResources>();
	}
}
