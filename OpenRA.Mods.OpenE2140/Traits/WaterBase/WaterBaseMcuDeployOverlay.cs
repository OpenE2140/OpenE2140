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

using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Orders;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.WaterBase;

public class WaterBaseMcuDeployOverlayInfo : TraitInfo<WaterBaseMcuDeployOverlay>, Requires<WaterBaseTransformsInfo> { }

public class WaterBaseMcuDeployOverlay : ICustomMcuDeployOverlayGenerator
{
	ICustomMcuDeployOverlay ICustomMcuDeployOverlayGenerator.CreateOverlay(Actor self, WorldRenderer wr, ActorInfo _)
	{
		return new WaterBaseMcuDeployOverlayRenderer(self, wr);
	}
}

public class WaterBaseMcuDeployOverlayRenderer : ICustomMcuDeployOverlay
{
	private readonly WaterBaseTransforms transforms;
	private readonly Sprite validCell;
	private readonly float validAlpha;

	public WaterBaseMcuDeployOverlayRenderer(Actor self, WorldRenderer wr)
	{
		this.transforms = self.Trait<WaterBaseTransforms>();

		var sequences = wr.World.Map.Sequences;
		var validSequence = sequences.GetSequence("overlay", "build-valid");
		this.validCell = validSequence.GetSprite(0);
		this.validAlpha = validSequence.GetAlpha(0);
	}

	IEnumerable<IRenderable> ICustomMcuDeployOverlay.Render(Actor self, WorldRenderer wr, CPos topLeft, Dictionary<CPos, PlaceBuildingCellType> footprint)
	{
		foreach (var cell in this.transforms.GetPossibleCellsForDockPlacement())
		{
			// If Main Building cannot be placed at this cell, don't render overlay sprite for the dock placement here.
			// It's redundant (because Water Base cannot be deployed anyway) and looks bad, when both sprites are rendered over each other.
			if (footprint.TryGetValue(cell, out var cellType) && cellType == PlaceBuildingCellType.Invalid)
				continue;

			yield return new SpriteRenderable(this.validCell, wr.World.Map.CenterOfCell(cell), WVec.Zero, -511, null, 1f, this.validAlpha, float3.Ones, TintModifiers.IgnoreWorldTint, true);
		}
	}

	IEnumerable<IRenderable> ICustomMcuDeployOverlay.RenderAnnotations(Actor self, WorldRenderer wr, CPos topLeft, Dictionary<CPos, PlaceBuildingCellType> footprint)
	{
		var color = this.transforms.GetPossibleCellsForDockPlacement().Any() ? Color.Green : Color.Red;

		yield return new CircleAnnotationRenderable(this.transforms.GetCenterOfFootprint(), this.transforms.Info.MaximumDockDistance, 1, color);
	}
}
