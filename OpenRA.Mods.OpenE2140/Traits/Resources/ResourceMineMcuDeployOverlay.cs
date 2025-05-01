using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;
using McuTransformsInfo = OpenRA.Mods.OpenE2140.Traits.Mcu.TransformsInfo;

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

public class ResourceMineMcuDeployOverlayInfo : TraitInfo<ResourceMineMcuDeployOverlayInfo>, Requires<McuTransformsInfo>
{
	[Desc("Color of mining area border when there's at least one minable cell inside it.")]
	public readonly Color MinableCellsInMiningAreaBorderColor = Color.Yellow;

	[Desc("Color of mining area border when there's no minable cell inside it.")]
	public readonly Color NoMinableCellsMiningAreaBorderColor = Color.Red;

	[Desc("Shape of mining area border.")]
	public readonly MiningAreaBorderShape MinableAreaBorderShape = MiningAreaBorderShape.Circle;

	public override object Create(ActorInitializer init)
	{
		return new ResourceMineMcuDeployOverlay(this);
	}
}

public class ResourceMineMcuDeployOverlay : ICustomMcuDeployOverlayGenerator
{
	private readonly ResourceMineMcuDeployOverlayInfo info;

	public ResourceMineMcuDeployOverlay(ResourceMineMcuDeployOverlayInfo resourceMineMcuDeployOverlayInfo)
	{
		this.info = resourceMineMcuDeployOverlayInfo;
	}

	ICustomMcuDeployOverlay ICustomMcuDeployOverlayGenerator.CreateOverlay(Actor self, WorldRenderer wr, ActorInfo _)
	{
		return new ResourceMineMcuDeployOverlayRenderer(self, wr, this.info);
	}
}

public class ResourceMineMcuDeployOverlayRenderer : ICustomMcuDeployOverlay
{
	private readonly ResourceMineMcuDeployOverlayInfo info;
	private readonly ICustomBuildingInfo customBuildingInfo;
	private readonly ResourceMineInfo resourceMineInfo;
	private readonly IResourceLayer resourceLayer;
	private readonly Sprite validCell;
	private readonly float validAlpha;

	public ResourceMineMcuDeployOverlayRenderer(Actor self, WorldRenderer wr, ResourceMineMcuDeployOverlayInfo info)
	{
		this.info = info;

		var transforms = self.Info.TraitInfo<McuTransformsInfo>();
		var mineActorInfo = self.World.Map.Rules.Actors[transforms.IntoActor];
		this.customBuildingInfo = mineActorInfo.TraitInfo<ICustomBuildingInfo>();
		this.resourceMineInfo = mineActorInfo.TraitInfo<ResourceMineInfo>();

		var sequences = wr.World.Map.Sequences;
		var validSequence = sequences.GetSequence("overlay", "build-resources");

		this.resourceLayer = self.World.WorldActor.TraitOrDefault<IResourceLayer>();
		this.validCell = validSequence.GetSprite(0);
		this.validAlpha = validSequence.GetAlpha(0);
	}

	IEnumerable<IRenderable> ICustomMcuDeployOverlay.Render(Actor self, WorldRenderer wr, CPos topLeft, Dictionary<CPos, PlaceBuildingCellType> footprint)
	{
		var centerCell = self.World.Map.CellContaining(this.customBuildingInfo.GetCenterOfFootprint(topLeft));
		var minableCells = this.resourceMineInfo.GetCellsInMiningArea(centerCell);

		foreach (var cell in minableCells)
		{
			if (this.resourceLayer.GetResource(cell).Density == 0)
				continue;

			yield return new SpriteRenderable(
				this.validCell, wr.World.Map.CenterOfCell(cell), WVec.Zero, -511, null, 1f,
				this.validAlpha, float3.Ones, TintModifiers.IgnoreWorldTint, true);
		}
	}

	IEnumerable<IRenderable> ICustomMcuDeployOverlay.RenderAnnotations(Actor self, WorldRenderer wr, CPos topLeft, Dictionary<CPos, PlaceBuildingCellType> footprint)
	{
		var footprintCenter = this.customBuildingInfo.GetCenterOfFootprint(topLeft);

		var minableCells = this.resourceMineInfo.GetCellsInMiningArea(self.World.Map.CellContaining(footprintCenter))
			.Where(p => this.resourceLayer.GetResource(p).Density > 0);

		// Color of the border is determined based on whether there are any minable cells (i.e. cells with ore)
		var color =
			minableCells.Any() ?
			this.info.MinableCellsInMiningAreaBorderColor
			: this.info.NoMinableCellsMiningAreaBorderColor;

		var borderRange = WDist.FromCells(this.resourceMineInfo.Range) + new WDist(512);

		switch (this.info.MinableAreaBorderShape)
		{
			case MiningAreaBorderShape.Square:
			{
				var tl = footprintCenter + new WVec(-borderRange, -borderRange, WDist.Zero);
				yield return new PolygonAnnotationRenderable(
					[
						tl,
						footprintCenter + new WVec(borderRange, -borderRange, WDist.Zero),
						footprintCenter + new WVec(borderRange, borderRange, WDist.Zero),
						footprintCenter + new WVec(-borderRange, borderRange, WDist.Zero),
					], tl, 1, color);
				break;
			}
			case MiningAreaBorderShape.Circle:
			{
				yield return new CircleAnnotationRenderable(footprintCenter, borderRange, 1, color);
				break;
			}
		}
	}
}
