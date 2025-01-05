using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits;

public class DummyResourceLayerInfo : TraitInfo, IResourceLayerInfo
{
	public override object Create(ActorInitializer init)
	{
		return new DummyResourceLayer(this);
	}

	bool IResourceLayerInfo.TryGetResourceIndex(string resourceType, out byte index)
	{
		index = 0;
		return false;
	}

	bool IResourceLayerInfo.TryGetTerrainType(string resourceType, out string terrainType)
	{
		terrainType = string.Empty;
		return false;
	}
}

public class DummyResourceLayer : IResourceLayer
{
	private readonly DummyResourceLayerInfo info;

	public DummyResourceLayer(DummyResourceLayerInfo info)
	{
		this.info = info;
	}

	bool IResourceLayer.IsEmpty => true;

	IResourceLayerInfo IResourceLayer.Info => this.info;

	event Action<CPos, string> IResourceLayer.CellChanged { add { } remove { } }

	int IResourceLayer.AddResource(string resourceType, CPos cell, int amount)
	{
		return 0;
	}

	bool IResourceLayer.CanAddResource(string resourceType, CPos cell, int amount)
	{
		return false;
	}

	void IResourceLayer.ClearResources(CPos cell)
	{
	}

	int IResourceLayer.GetMaxDensity(string resourceType)
	{
		return 0;
	}

	ResourceLayerContents IResourceLayer.GetResource(CPos cell)
	{
		return new ResourceLayerContents();
	}

	bool IResourceLayer.IsVisible(CPos cell)
	{
		return false;
	}

	int IResourceLayer.RemoveResource(string resourceType, CPos cell, int amount)
	{
		return 0;
	}
}
