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
using OpenRA.Primitives;

namespace OpenRA.Mods.OpenE2140.Traits;

public class WaterBaseDockLinkRenderable : IRenderable, IFinalizedRenderable
{
	private readonly WPos end;
	private readonly float width;
	private readonly float markerWidth;
	private readonly Color color;

	public WaterBaseDockLinkRenderable(WPos start, WPos end, float width, float markerWidth, Color color)
	{
		this.Pos = start;
		this.end = end;
		this.width = width;
		this.markerWidth = markerWidth;
		this.color = color;
	}

	public WPos Pos { get; }
	public int ZOffset => 0;
	public bool IsDecoration => true;

	public IRenderable WithZOffset(int newOffset) { return new LineAnnotationRenderable(this.Pos, this.end, this.width, this.color); }
	public IRenderable OffsetBy(in WVec vec) { return new LineAnnotationRenderable(this.Pos + vec, this.end + vec, this.width, this.color); }
	public IRenderable AsDecoration() { return this; }

	public IFinalizedRenderable PrepareRender(WorldRenderer wr) { return this; }
	public void Render(WorldRenderer wr)
	{
		var startPos = wr.Viewport.WorldToViewPx(wr.ScreenPosition(this.Pos));
		var endPos = wr.Viewport.WorldToViewPx(wr.Screen3DPosition(this.end));
		Game.Renderer.RgbaColorRenderer.DrawLine(
			startPos,
			endPos,
			this.width, this.color, this.color);

		DrawTargetMarker(this.color, startPos, this.markerWidth);
		DrawTargetMarker(this.color, endPos, this.markerWidth);
	}

	public static void DrawTargetMarker(Color color, int2 screenPos, float size)
	{
		var offset = new float2(size, size);
		var tl = screenPos - offset;
		var br = screenPos + offset;
		Game.Renderer.RgbaColorRenderer.FillRect(tl, br, color);
	}

	public void RenderDebugGeometry(WorldRenderer wr) { }
	public Rectangle ScreenBounds(WorldRenderer wr) { return Rectangle.Empty; }
}
