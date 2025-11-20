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

[Desc("Hides actors, which prerequisite technologies can't be researched due to research limit restriction. Attach to all factory buildings.")]
public class HideUnbuildableActorsInfo : TraitInfo<HideUnbuildableActors>, Requires<ProductionQueueInfo> { }

public class HideUnbuildableActors : INotifyAddedToWorld, INotifyOwnerChanged
{
	void INotifyAddedToWorld.AddedToWorld(Actor self)
	{
		var research = self.Owner.PlayerActor.Trait<Research>();

		research.HideUnbuildableActors(self);
	}

	void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
	{
		var research = newOwner.PlayerActor.Trait<Research>();

		research.HideUnbuildableActors(self);
	}
}
