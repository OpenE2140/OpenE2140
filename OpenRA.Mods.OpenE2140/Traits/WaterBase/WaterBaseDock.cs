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

using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.WaterBase;

public class WaterBaseDockInfo : TraitInfo
{
	public override object Create(ActorInitializer init)
	{
		return new WaterBaseDock(init, this);
	}
}

public class WaterBaseDock
{
	private readonly Actor self;

	// TODO: shouldn't this be not null?
	private WaterBaseBuilding? waterBaseBuilding;

	public WaterBaseDock(ActorInitializer init, WaterBaseDockInfo info)
	{
		this.self = init.Self;
		// TODO: there should always be this init object (either from map, or passed when Dock actor is created in WaterBaseBuilding
		var waterBaseActor = init.GetOrDefault<WaterBaseDockInit>(info);
		if (waterBaseActor != null)
		{
			init.World.AddFrameEndTask(_ =>
			{
				var buildingActor = waterBaseActor.Value.Actor(init.World).Value;
				this.waterBaseBuilding = buildingActor.Trait<WaterBaseBuilding>();
				this.waterBaseBuilding.AssignDock(buildingActor, init.Self);
			});
		}
	}
}

public class WaterBaseDockInit : ValueActorInit<ActorInitActorReference>, ISingleInstanceInit
{
	public WaterBaseDockInit(Actor actor)
		: base(actor) { }
}

