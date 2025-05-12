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

using System.Diagnostics.CodeAnalysis;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Mcu;

public static class McuUtils
{
	public static bool TryGetTargetBuilding(OpenRA.World world, ActorInfo mcuActor, [NotNullWhen(true)] out ActorInfo? buildingActor)
	{
		buildingActor = null;
		if (!mcuActor.HasTraitInfo<McuInfo>())
			return false;

		var targetActor = GetTargetBuilding(mcuActor);
		buildingActor = targetActor != null ? world.Map.Rules.Actors[targetActor] : null;
		return buildingActor != null;
	}

	public static ActorInfo? GetTargetBuilding(OpenRA.World world, ActorInfo mcuActor)
	{
		if (!mcuActor.HasTraitInfo<McuInfo>())
			throw new ArgumentException($"Actor '{mcuActor.Name}' does not have Mcu trait (maybe it's not an MCU?)", nameof(mcuActor));

		var targetActor = GetTargetBuilding(mcuActor);

		return targetActor != null ? world.Map.Rules.Actors[targetActor] : null;
	}

	public static ActorInfo? GetMcuActor(OpenRA.World world, ActorInfo buildingActor)
	{
		if (!buildingActor.HasTraitInfo<BuildingInfo>())
			throw new ArgumentException($"Actor '{buildingActor.Name}' does not have Building trait (maybe it's not a building?)", nameof(buildingActor));

		return world.Map.Rules.Actors.Values
			.Where(a => a.HasTraitInfo<McuInfo>())
			.FirstOrDefault(a => GetTargetBuilding(a) == buildingActor.Name);
	}

	private static string? GetTargetBuilding(ActorInfo mcuActor)
	{
		return mcuActor.TraitInfoOrDefault<ITransformsInfo>()?.IntoActor;
	}
}
