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

using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.WaterBase;

public class WaterBaseBuildingInfo : TraitInfo
{
	public override object Create(ActorInitializer init)
	{
		return new WaterBaseBuilding(init.Self);
	}
}

public class WaterBaseBuilding : INotifyOwnerChanged
{
	private readonly Actor self;

	public Actor? DockActor { get; private set; }

	private WaterBaseDock? waterBaseDock;

	public WaterBaseBuilding(Actor self)
	{
		this.self = self;
	}

	internal void AssignDock(Actor self, Actor dockActor)
	{
		this.DockActor = dockActor;
        this.waterBaseDock = dockActor.Trait<WaterBaseDock>();

        if (self.Owner != dockActor.Owner)
			dockActor.ChangeOwner(self.Owner);
	}

	void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
	{
		if (this.DockActor?.Owner != newOwner)
			this.DockActor?.ChangeOwner(newOwner);
	}
}
