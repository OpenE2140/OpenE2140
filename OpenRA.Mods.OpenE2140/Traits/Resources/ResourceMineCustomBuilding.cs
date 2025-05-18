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

using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.OpenE2140.Traits.Mcu;

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

public class ResourceMineCustomBuildingInfo : CustomBuildingInfo
{
	public override Dictionary<CPos, PlaceBuildingCellType> GetBuildingPlacementFootprint(OpenRA.World world, CPos cell, Actor toIgnore)
	{
		var footprint = base.GetBuildingPlacementFootprint(world, cell, toIgnore);

		var footprintCellTypes = new Dictionary<string, int>();
		foreach ((var c, var type) in footprint)
		{
			var terrainType = world.Map.GetTerrainInfo(c).Type;
			footprintCellTypes.TryGetValue(terrainType, out var count);
			footprintCellTypes[terrainType] = ++count;
		}

		// Next check, if cells of the footprint contain valid terrain types
		if (this.AllowedTerrainTypesCondition?.Evaluate(footprintCellTypes) == false)
			return footprint.Keys.ToDictionary(c => c, _ => PlaceBuildingCellType.Invalid);

		return footprint;
	}
}
