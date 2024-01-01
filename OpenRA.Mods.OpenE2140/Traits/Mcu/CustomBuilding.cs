using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Common.Traits;
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
		return this.buildingInfo?.Tiles(location) ?? Enumerable.Empty<CPos>();
	}

	public WPos GetCenterOfFootprint(CPos location)
	{
		var footprint = this.Tiles(location);
		var (topLeft, bottomRight) = GetBounds(footprint);

		return topLeft + (bottomRight - topLeft) / 2;
	}

	private static (WPos topLeft, WPos bottomRight) GetBounds(IEnumerable<CPos> cells)
	{
		var left = int.MaxValue;
		var right = int.MinValue;
		var top = int.MaxValue;
		var bottom = int.MinValue;

		foreach (var cell in cells)
		{
			left = Math.Min(left, cell.X);
			right = Math.Max(right, cell.X);
			top = Math.Min(top, cell.Y);
			bottom = Math.Max(bottom, cell.Y);
		}

		return (new WPos(1024 * left, 1024 * top, 0),
			new WPos(1024 * right + 1024, 1024 * bottom + 1024, 0));
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
