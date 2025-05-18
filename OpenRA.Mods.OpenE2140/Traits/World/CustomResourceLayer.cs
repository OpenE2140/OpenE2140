using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Extensions;

namespace OpenRA.Mods.OpenE2140.Traits.World;

public class CustomResourceLayerInfo : ResourceLayerInfo
{
	public override object Create(ActorInitializer init)
	{
		return new CustomResourceLayer(init.Self, this);
	}
}

public class CustomResourceLayer : ResourceLayer
{
	private readonly ResourceLayerInfo info;

	public CustomResourceLayer(Actor self, ResourceLayerInfo info)
		: base(self, info)
	{
		this.info = info;
	}

	protected override bool AllowResourceAt(string resourceType, CPos cell)
	{
		if (!this.Map.Contains(cell) || this.Map.Ramp[cell] != 0)
			return false;

		if (resourceType == null || !this.info.ResourceTypes.TryGetValue(resourceType, out var resourceInfo))
			return false;

		if (!resourceInfo.AllowedTerrainTypes.Contains(this.Map.GetTerrainInfo(cell).Type))
			return false;

		return this.BuildingInfluence.GetBuildingsAt(cell).All(a => !a.TryGetTrait<Building>(out var building) || building.Info.AllowPlacementOnResources);
	}
}
