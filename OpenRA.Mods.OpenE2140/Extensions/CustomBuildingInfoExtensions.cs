using OpenRA.Mods.OpenE2140.Traits;
using OpenRA.Mods.OpenE2140.Traits.Mcu;

namespace OpenRA.Mods.OpenE2140.Extensions;

public static class CustomBuildingInfoExtensions
{
	public static CPos GetCenterCellOfFootprint(this ICustomBuildingInfo customBuildingInfo, World world, CPos location)
	{
		return world.Map.CellContaining(customBuildingInfo.GetCenterOfFootprint(location));
	}

	public static CPos GetCenterCellOfFootprint(this Actor buildingActor)
	{
		var customBuildingInfo = CustomBuildingInfoWrapper.WrapIfNecessary(buildingActor.Info);
		if (customBuildingInfo != null)
		{
			return customBuildingInfo.GetCenterCellOfFootprint(buildingActor.World, buildingActor.Location);
		}

		throw new ArgumentException($"Actor {buildingActor} doesn't have any building trait", nameof(buildingActor));
	}
}
