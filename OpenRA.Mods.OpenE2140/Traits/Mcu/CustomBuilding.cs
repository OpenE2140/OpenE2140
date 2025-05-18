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
using OpenRA.Support;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Mcu;

public class CustomBuildingInfo : TraitInfo<CustomBuilding>, IRulesetLoaded, Requires<BuildingInfo>, ICustomBuildingInfo
{
	protected ActorInfo? actorInfo;
	protected BuildingInfo? buildingInfo;

	[Desc("Boolean expression defining condition of terrain types, where the the Building can be built.",
		$"Currently requires that all terrain types used in the condition are also defined in the {nameof(BuildingInfo)}.{nameof(BuildingInfo.TerrainTypes)} field.")]
	public readonly BooleanExpression? AllowedTerrainTypesCondition = null;

	public virtual bool IsCellBuildable(OpenRA.World world, CPos cell, Actor? toIgnore = null)
	{
		return world.IsCellBuildable(cell, this.actorInfo, this.buildingInfo, toIgnore);
	}

	public virtual bool CanPlaceBuilding(OpenRA.World world, CPos cell, Actor toIgnore)
	{
		if (this.buildingInfo == null)
			return false;

		if (this.AllowedTerrainTypesCondition != null)
		{
			// TODO: refactoring: extract and unify with GetBuildingPlacementFootprint in WaterBaseBuildingInfo?
			var footprintCells = this.buildingInfo.Tiles(cell).ToList();
			var footprintCellTypes = new Dictionary<string, int>();
			foreach (var c in footprintCells)
			{
				var terrainType = world.Map.GetTerrainInfo(c).Type;
				footprintCellTypes.TryGetValue(terrainType, out var count);
				footprintCellTypes[terrainType] = ++count;
			}

			// Next check, if cells of the footprint contain valid terrain types
			if (this.AllowedTerrainTypesCondition.Evaluate(footprintCellTypes) == false)
				return false;
		}

		return world.CanPlaceBuilding(cell, this.actorInfo, this.buildingInfo, toIgnore);
	}

	public virtual Dictionary<CPos, PlaceBuildingCellType> GetBuildingPlacementFootprint(OpenRA.World world, CPos cell, Actor toIgnore)
	{
		var footprint = new Dictionary<CPos, PlaceBuildingCellType>();

		if (this.buildingInfo == null)
			return footprint;

		foreach (var t in this.buildingInfo.Tiles(cell))
		{
			footprint.Add(t, this.IsCellBuildable(world, t, toIgnore) ? PlaceBuildingCellType.Valid : PlaceBuildingCellType.Invalid);
		}

		return footprint;
	}

	public virtual IEnumerable<CPos> Tiles(CPos location)
	{
		return this.buildingInfo?.Tiles(location) ?? [];
	}

	public WPos GetCenterOfFootprint(CPos location)
	{
		var (topLeft, bottomRight) = this.Tiles(location).GetBounds();

		return topLeft + (bottomRight - topLeft) / 2;
	}

	void IRulesetLoaded<ActorInfo>.RulesetLoaded(Ruleset rules, ActorInfo info)
	{
		if (rules.TerrainInfo == null)
			return;

		this.actorInfo = info;
		this.buildingInfo = info.TraitInfo<BuildingInfo>();
	}
}

public class CustomBuilding { }
