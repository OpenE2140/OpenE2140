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
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Extensions;

namespace OpenRA.Mods.OpenE2140.Traits;

public class CustomBuildingInfoWrapper : ICustomBuildingInfo
{
	private readonly BuildingInfo buildingInfo;
	private readonly ActorInfo actorInfo;

	public CustomBuildingInfoWrapper(ActorInfo actorInfo)
	{
		this.actorInfo = actorInfo;
		this.buildingInfo = actorInfo.TraitInfo<BuildingInfo>();
	}

	public bool CanPlaceBuilding(World world, CPos cell, Actor toIgnore)
	{
		return world.CanPlaceBuilding(cell, this.actorInfo, this.buildingInfo, toIgnore);
	}

	public bool IsCellBuildable(World world, CPos cell, Actor? toIgnore)
	{
		return world.IsCellBuildable(cell, this.actorInfo, this.buildingInfo, toIgnore);
	}

	public Dictionary<CPos, PlaceBuildingCellType> GetBuildingPlacementFootprint(World world, CPos cell, Actor toIgnore)
	{
		var footprint = new Dictionary<CPos, PlaceBuildingCellType>();

		foreach (var t in this.buildingInfo.Tiles(cell))
		{
			footprint.Add(t, this.IsCellBuildable(world, t, toIgnore) ? PlaceBuildingCellType.Valid : PlaceBuildingCellType.Invalid);
		}

		return footprint;
	}

	public IEnumerable<CPos> Tiles(CPos location)
	{
		return this.buildingInfo.Tiles(location);
	}

	public WPos GetCenterOfFootprint(CPos location)
	{
		var (topLeft, bottomRight) = this.Tiles(location).GetBounds();

		return topLeft + (bottomRight - topLeft) / 2;
	}

	public static ICustomBuildingInfo? WrapIfNecessary(ActorInfo actorInfo)
	{
		if (actorInfo.TryGetTrait<CustomBuildingInfo>(out var customBuildingInfo))
			return customBuildingInfo;

		if (actorInfo.HasTraitInfo<BuildingInfo>())
			return new CustomBuildingInfoWrapper(actorInfo);

		return null;
	}
}
