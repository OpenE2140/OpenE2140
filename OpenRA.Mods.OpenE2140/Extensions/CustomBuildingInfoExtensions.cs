using OpenRA.Mods.OpenE2140.Traits;

namespace OpenRA.Mods.OpenE2140.Extensions;

public static class CustomBuildingInfoExtensions
{
	public static CPos GetCenterCellOfFootprint(this ICustomBuildingInfo customBuildingInfo, World world, CPos location)
	{
		return world.Map.CellContaining(customBuildingInfo.GetCenterOfFootprint(location));
	}
}
