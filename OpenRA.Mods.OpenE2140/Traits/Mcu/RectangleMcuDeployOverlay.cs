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

namespace OpenRA.Mods.OpenE2140.Traits.Mcu;

public class BuildingOverlayRectangleRenderable : IRenderable, IFinalizedRenderable
{
	private readonly Rectangle decorationBounds;
	private readonly Color color;

	public BuildingOverlayRectangleRenderable(Actor actor, Rectangle decorationBounds, Color color)
		: this(actor.CenterPosition, decorationBounds, color) { }

	public BuildingOverlayRectangleRenderable(WPos pos, Rectangle decorationBounds, Color color)
	{
		this.Pos = pos;
		this.decorationBounds = decorationBounds;
		this.color = color;
	}

	public WPos Pos { get; }

	public int ZOffset => 0;
	public bool IsDecoration => true;

	public IRenderable WithZOffset(int newOffset) { return this; }
	public IRenderable OffsetBy(in WVec vec) { return new SelectionBoxAnnotationRenderable(this.Pos + vec, this.decorationBounds, this.color); }
	public IRenderable AsDecoration() { return this; }

	public IFinalizedRenderable PrepareRender(WorldRenderer wr) { return this; }
	public void Render(WorldRenderer wr)
	{
		var tl = wr.Viewport.WorldToViewPx(this.decorationBounds.TopLeft).ToFloat2();
		var br = wr.Viewport.WorldToViewPx(this.decorationBounds.BottomRight).ToFloat2();
		var tr = new float2(br.X, tl.Y);
		var bl = new float2(tl.X, br.Y);
		var u = new float2(24, 0);
		var v = new float2(0, 24);

		var cr = Game.Renderer.RgbaColorRenderer;
		cr.DrawLine(new float3[] { tl + u, tl, tl + v }, 1, this.color, true);
		cr.DrawLine(new float3[] { tr - u, tr, tr + v }, 1, this.color, true);
		cr.DrawLine(new float3[] { br - u, br, br - v }, 1, this.color, true);
		cr.DrawLine(new float3[] { bl + u, bl, bl - v }, 1, this.color, true);
	}

	public void RenderDebugGeometry(WorldRenderer wr) { }
	public Rectangle ScreenBounds(WorldRenderer wr) { return Rectangle.Empty; }
}
