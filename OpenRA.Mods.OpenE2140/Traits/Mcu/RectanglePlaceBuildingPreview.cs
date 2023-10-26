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
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Mcu;

public class RectanglePlaceBuildingPreviewInfo : TraitInfo<RectanglePlaceBuildingPreview>, IPlaceBuildingPreviewGeneratorInfo
{
	IPlaceBuildingPreview IPlaceBuildingPreviewGeneratorInfo.CreatePreview(WorldRenderer wr, ActorInfo ai, TypeDictionary init)
	{
		return new RectanglePlaceBuildingPreviewPreview(wr, ai);
	}
}

public class RectanglePlaceBuildingPreview { }

public class RectanglePlaceBuildingPreviewPreview : IPlaceBuildingPreview
{
	private readonly WVec centerOffset;
	private readonly int2 topLeftScreenOffset;

	public RectanglePlaceBuildingPreviewPreview(WorldRenderer wr, ActorInfo ai)
	{
		var world = wr.World;
		this.centerOffset = ai.TraitInfo<BuildingInfo>().CenterOffset(world);
		this.topLeftScreenOffset = -wr.ScreenPxOffset(this.centerOffset);
	}

	IEnumerable<IRenderable> IPlaceBuildingPreview.Render(WorldRenderer wr, CPos topLeft, Dictionary<CPos, PlaceBuildingCellType> footprint)
	{
		var b = GetBounds(footprint);
		var rect = Rectangle.FromLTRB(b.topLeft.X, b.topLeft.Y, b.bottomRight.X, b.bottomRight.Y);

		// Convert from WDist to pixels
		var size = new int2(rect.Width * wr.TileSize.Width / wr.TileScale, rect.Height * wr.TileSize.Height / wr.TileScale);

		var xy = wr.ScreenPxPosition(b.topLeft);
		var renderRect = new Rectangle(xy, new Size(size.X, size.Y));

		var color = footprint.Values.Any(c => c.HasFlag(PlaceBuildingCellType.Invalid)) ? Color.Red : Color.Green;

		yield return new BuildingPreviewRectangleRenderable(b.topLeft, renderRect, color);
	}

	private static (WPos topLeft, WPos bottomRight) GetBounds(Dictionary<CPos, PlaceBuildingCellType> footprint)
	{
		var left = int.MaxValue;
		var right = int.MinValue;
		var top = int.MaxValue;
		var bottom = int.MinValue;

		foreach (var p in footprint.Keys)
		{
			left = Math.Min(left, p.X);
			right = Math.Max(right, p.X);
			top = Math.Min(top, p.Y);
			bottom = Math.Max(bottom, p.Y);
		}

		return (new WPos(1024 * left, 1024 * top, 0),
			new WPos(1024 * right + 1024, 1024 * bottom + 1024, 0));
	}

	IEnumerable<IRenderable> IPlaceBuildingPreview.RenderAnnotations(WorldRenderer wr, CPos topLeft)
	{
		return Enumerable.Empty<IRenderable>();
	}

	void IPlaceBuildingPreview.Tick() { }

	int2 IPlaceBuildingPreview.TopLeftScreenOffset => this.topLeftScreenOffset;
}

