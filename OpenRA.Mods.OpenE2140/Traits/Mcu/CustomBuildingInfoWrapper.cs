using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Extensions;

namespace OpenRA.Mods.OpenE2140.Traits.Mcu;

public class CustomBuildingInfoWrapper : ICustomBuildingInfo
{
	private readonly BuildingInfo buildingInfo;
	private readonly ActorInfo actorInfo;

	public CustomBuildingInfoWrapper(ActorInfo actorInfo)
	{
		this.actorInfo = actorInfo;
		this.buildingInfo = actorInfo.TraitInfo<BuildingInfo>();
	}

	public bool CanPlaceBuilding(OpenRA.World world, CPos cell, Actor toIgnore)
	{
		return world.CanPlaceBuilding(cell, this.actorInfo, this.buildingInfo, toIgnore);
	}

	public bool IsCellBuildable(OpenRA.World world, CPos cell, Actor? toIgnore)
	{
		return world.IsCellBuildable(cell, this.actorInfo, this.buildingInfo, toIgnore);
	}

	public Dictionary<CPos, PlaceBuildingCellType> GetBuildingPlacementFootprint(OpenRA.World world, CPos cell, Actor toIgnore)
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

	public static ICustomBuildingInfo? WrapIfNecessary(ActorInfo actorInfo)
	{
		if (actorInfo.TryGetTrait<CustomBuildingInfo>(out var customBuildingInfo))
			return customBuildingInfo;

		if (actorInfo.HasTraitInfo<BuildingInfo>())
			return new CustomBuildingInfoWrapper(actorInfo);

		return null;
	}
}
