using OpenRA.Graphics;
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;
using McuTransformsInfo = OpenRA.Mods.OpenE2140.Traits.Mcu.TransformsInfo;

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

public class ResourceMineMcuDeployOverlayInfo : TraitInfo<ResourceMineMcuDeployOverlayInfo>, Requires<McuTransformsInfo>
{
	public override object Create(ActorInitializer init)
	{
		return new ResourceMineMcuDeployOverlay();
	}
}

public class ResourceMineMcuDeployOverlay : ICustomMcuDeployOverlayGenerator
{
	ICustomMcuDeployOverlay ICustomMcuDeployOverlayGenerator.CreateOverlay(Actor self, WorldRenderer wr, ActorInfo _)
	{
		return new ResourceMineMcuDeployOverlayRenderer(self, wr);
	}
}

public class ResourceMineMcuDeployOverlayRenderer : ICustomMcuDeployOverlay
{
	private readonly ICustomBuildingInfo customBuildingInfo;
	private readonly IResourceLayer resourceLayer;
	private readonly Sprite validCell;
	private readonly float validAlpha;

	public ResourceMineMcuDeployOverlayRenderer(Actor self, WorldRenderer wr)
	{
		var transforms = self.Info.TraitInfo<McuTransformsInfo>();
		var mineActorInfo = self.World.Map.Rules.Actors[transforms.IntoActor];
		this.customBuildingInfo = mineActorInfo.TraitInfo<ICustomBuildingInfo>();

		var sequences = wr.World.Map.Sequences;
		var validSequence = sequences.GetSequence("overlay", "build-resources");

		this.resourceLayer = self.World.WorldActor.TraitOrDefault<IResourceLayer>();
		this.validCell = validSequence.GetSprite(0);
		this.validAlpha = validSequence.GetAlpha(0);
	}

	IEnumerable<IRenderable> ICustomMcuDeployOverlay.Render(Actor self, WorldRenderer wr, CPos topLeft, Dictionary<CPos, PlaceBuildingCellType> footprint)
	{
		foreach (var cell in this.customBuildingInfo.Tiles(topLeft))
		{
			if (this.resourceLayer.GetResource(cell).Density == 0)
				continue;

			yield return new SpriteRenderable(this.validCell, wr.World.Map.CenterOfCell(cell), WVec.Zero, -511, null, 1f, this.validAlpha, float3.Ones, TintModifiers.IgnoreWorldTint, true);
		}
	}

	IEnumerable<IRenderable> ICustomMcuDeployOverlay.RenderAnnotations(Actor self, WorldRenderer wr, CPos topLeft, Dictionary<CPos, PlaceBuildingCellType> footprint)
	{
		return Enumerable.Empty<IRenderable>();
	}
}
