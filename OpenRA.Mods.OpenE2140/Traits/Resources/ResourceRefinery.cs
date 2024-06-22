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
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

[UsedImplicitly]
[Desc("This actor can accept resource crates and process them.")]
public class ResourceRefineryInfo : ConveyorBeltInfo
{
	public override object Create(ActorInitializer init)
	{
		return new ResourceRefinery(init.Self, this);
	}
}

public class ResourceRefinery : ConveyorBelt, INotifyAddedToWorld, INotifyOwnerChanged
{
	private PlayerResources? playerResources;

	public ResourceRefinery(Actor self, ConveyorBeltInfo info)
		: base(self, info)
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

	// TODO temporary => must come from crate transporter
	protected override void TickInner(Actor self)
	{
		base.TickInner(self);

		// TODO: support trait pausing (necessary for power management)
		//if (this.IsTraitDisabled || this.IsTraitPaused)
		if (this.IsTraitDisabled)
			return;

		if (this.crate != null || self.World.WorldTick % 100 != 0)
			return;

		this.Activate(
			self,
			self.World.CreateActor(
					false,
					"crate",
					new TypeDictionary { new ParentActorInit(self), new LocationInit(self.Location), new OwnerInit(self.Owner) }
				)
				.Trait<ResourceCrate>()
		);

		if (this.crate != null)
			this.crate.Resources = 500;
	}

	protected override void Complete(Actor self)
	{
		if (this.crate == null)
			return;

		this.playerResources?.GiveCash(this.crate.Resources);
		this.crate = null;
	}

	public override bool IsDockingPossible(Actor clientActor, IDockClient client, bool ignoreReservations = false)
	{
		if (!base.IsDockingPossible(clientActor, client, ignoreReservations))
			return false;

		return this.crate == null;
	}
}
