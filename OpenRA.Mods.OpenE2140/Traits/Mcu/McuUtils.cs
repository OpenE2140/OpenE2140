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

namespace OpenRA.Mods.OpenE2140.Traits.Mcu;

public static class McuUtils
{
	public static ActorInfo? GetTargetBuilding(OpenRA.World world, ActorInfo mcuActor)
	{
		if (!mcuActor.HasTraitInfo<McuInfo>())
			throw new ArgumentException($"Actor '{mcuActor.Name}' does not have Mcu trait (maybe it's not an MCU?)", nameof(mcuActor));

		var transforms = mcuActor.TraitInfos<TransformsInfo>().FirstOrDefault();

		return transforms != null ? world.Map.Rules.Actors[transforms.IntoActor] : null;
	}

	public static ActorInfo? GetMcuActor(OpenRA.World world, ActorInfo buildingActor)
	{
		if (!buildingActor.HasTraitInfo<BuildingInfo>())
			throw new ArgumentException($"Actor '{buildingActor.Name}' does not have Building trait (maybe it's not a building?)", nameof(buildingActor));

		return world.Map.Rules.Actors.Values
			.Where(a => a.HasTraitInfo<McuInfo>())
			.FirstOrDefault(a => a.TraitInfos<TransformsInfo>().Any(t => t.IntoActor == buildingActor.Name));
	}
}
