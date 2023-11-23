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
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Mcu;

public class RectangleMcuDeployOverlayInfo : TraitInfo<RectangleMcuDeployOverlay> { }

public class RectangleMcuDeployOverlay : ICustomMcuDeployOverlayGenerator
{
	ICustomMcuDeployOverlay ICustomMcuDeployOverlayGenerator.CreateOverlay(Actor self, WorldRenderer wr, ActorInfo intoActor)
	{
		return new RectangleMcuDeployOverlayRenderer();
	}
}

public class RectangleMcuDeployOverlayRenderer : ICustomMcuDeployOverlay
{
	IEnumerable<IRenderable> ICustomMcuDeployOverlay.Render(Actor self, WorldRenderer wr, CPos topLeft, Dictionary<CPos, PlaceBuildingCellType> footprint)
	{
		return Enumerable.Empty<IRenderable>();
	}

	IEnumerable<IRenderable> ICustomMcuDeployOverlay.RenderAnnotations(Actor self, WorldRenderer wr, CPos topLeft, Dictionary<CPos, PlaceBuildingCellType> footprint)
	{
		var b = GetBounds(footprint);

		var size = wr.ScreenPxPosition(b.bottomRight) - wr.ScreenPxPosition(b.topLeft);
		var renderRect = new Rectangle(wr.ScreenPxPosition(b.topLeft), new Size(size.X, size.Y));

		var color = footprint.Values.Any(c => c.HasFlag(PlaceBuildingCellType.Invalid)) ? Color.Red : Color.Green;

		yield return new BuildingOverlayRectangleRenderable(b.topLeft, renderRect, color);
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
}

