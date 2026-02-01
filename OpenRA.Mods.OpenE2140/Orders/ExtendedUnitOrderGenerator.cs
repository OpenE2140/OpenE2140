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
using OpenRA.Mods.OpenE2140.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Orders;

public class ExtendedUnitOrderGenerator : UnitOrderGenerator
{
	public ExtendedUnitOrderGenerator(World world)
		: base(world)
	{
	}

	public override IEnumerable<IRenderable> Render(WorldRenderer wr, World world)
	{
		return this.GetOrderPreviewRender(wr)?.Render(wr) ?? [];
	}

	public override IEnumerable<IRenderable> RenderAnnotations(WorldRenderer wr, World world)
	{
		return this.GetOrderPreviewRender(wr)?.RenderAnnotations(wr) ?? [];
	}

	public override IEnumerable<IRenderable> RenderAboveShroud(WorldRenderer wr, World world)
	{
		return this.GetOrderPreviewRender(wr)?.RenderAboveShroud(wr) ?? [];
	}

	private UnitOrderResultWrapper? GetOrderPreviewRender(WorldRenderer wr)
	{
		var screenPos = Viewport.LastMousePos;
		var cell = wr.Viewport.ViewToWorld(screenPos);
		var worldPixel = wr.Viewport.ViewToWorldPx(screenPos);

		var mi = new MouseInput
		{
			Location = screenPos,
			Button = this.ActionButton,
			Modifiers = Game.GetModifierKeys()
		};

		var target = TargetForInput(wr.World, cell, worldPixel, mi);

		var ordersWithPreview = wr.World.Selection.Actors
			.Select(a => this.OrderForUnit(a, target, cell, mi))
			.OfType<UnitOrderResult>()
			.Select(r => new UnitOrderResultWrapper(r))
			.Where(x => x.Order != null && x.OrderPreview != null);

		return ordersWithPreview.MaxByOrDefault(x => x.Order!.OrderPriority);
	}

	private record UnitOrderResultWrapper(UnitOrderResult UnitOrderResult)
	{
		public IOrderTargeter? Order => this.UnitOrderResult.Order;

		public IOrderPreviewRender? OrderPreview => this.UnitOrderResult.Trait as IOrderPreviewRender;

		public IEnumerable<IRenderable>? Render(WorldRenderer wr)
		{
			return this.OrderPreview?.Render(this.UnitOrderResult.Actor, wr);
		}

		public IEnumerable<IRenderable>? RenderAboveShroud(WorldRenderer wr)
		{
			return this.OrderPreview?.RenderAboveShroud(this.UnitOrderResult.Actor, wr);
		}

		public IEnumerable<IRenderable>? RenderAnnotations(WorldRenderer wr)
		{
			return this.OrderPreview?.RenderAnnotations(this.UnitOrderResult.Actor, wr);
		}
	}
}
