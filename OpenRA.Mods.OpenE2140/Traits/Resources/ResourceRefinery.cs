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
[Desc("This actor can accept resource crates and process them.")]
public class ResourceRefineryInfo : ConveyorBeltInfo
{
	public override object Create(ActorInitializer init)
	{
		return new ResourceRefinery(this);
	}
}

public class ResourceRefinery : ConveyorBelt, INotifyAddedToWorld, INotifyOwnerChanged
{
	private PlayerResources? playerResources;

	public ResourceRefinery(ConveyorBeltInfo info)
		: base(info)
	{
	}

	void INotifyAddedToWorld.AddedToWorld(Actor self)
	{
		this.playerResources = self.Owner.PlayerActor.TraitOrDefault<PlayerResources>();
	}

	void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
	{
		this.playerResources = newOwner.PlayerActor.TraitOrDefault<PlayerResources>();
	}

	protected override void Complete(Actor self)
	{
		if (this.crate == null)
			return;

		this.playerResources?.GiveCash(this.crate.Resources);
		this.crate = null;
	}
}
