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

using System.Reflection;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.OpenE2140.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Orders;

public class ExtendedUnitOrderGenerator : UnitOrderGenerator
{
	public override IEnumerable<IRenderable> Render(WorldRenderer wr, World world)
	{
		return GetOrderPreviewRender(wr)?.Render(wr) ?? Enumerable.Empty<IRenderable>();
	}

	public override IEnumerable<IRenderable> RenderAnnotations(WorldRenderer wr, World world)
	{
		return GetOrderPreviewRender(wr)?.RenderAnnotations(wr) ?? Enumerable.Empty<IRenderable>();
	}

	public override IEnumerable<IRenderable> RenderAboveShroud(WorldRenderer wr, World world)
	{
		return GetOrderPreviewRender(wr)?.RenderAboveShroud(wr) ?? Enumerable.Empty<IRenderable>();
	}

	private static UnitOrderResult? GetOrderPreviewRender(WorldRenderer wr)
	{
		var screenPos = Viewport.LastMousePos;
		var cell = wr.Viewport.ViewToWorld(screenPos);
		var worldPixel = wr.Viewport.ViewToWorldPx(screenPos);

		var mi = new MouseInput
		{
			Location = screenPos,
			Button = Game.Settings.Game.MouseButtonPreference.Action,
			Modifiers = Game.GetModifierKeys()
		};

		var target = TargetForInput(wr.World, cell, worldPixel, mi);

		var ordersWithPreview = wr.World.Selection.Actors
			.Select(a => OrderForUnit(a, target, cell, mi))
			.OfType<UnitOrderResult>()
			.Where(x => x.Order != null && x.OrderPreview != null);

		return ordersWithPreview.MaxByOrDefault(x => x.Order!.OrderPriority);
	}

	// TODO: PR to make these method protected (+ UnitOrderResult class)
	private static Target TargetForInput(World world, CPos cell, int2 worldPixel, MouseInput mi)
	{
		return (Target)typeof(UnitOrderGenerator).GetMethod("TargetForInput", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { world, cell, worldPixel, mi })!;
	}
	private static UnitOrderResult? OrderForUnit(Actor self, Target target, CPos xy, MouseInput mi)
	{
		var obj = typeof(UnitOrderGenerator).GetMethod("OrderForUnit", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { self, target, xy, mi })!;
		if (obj == null)
			return null;

		var type = obj.GetType();

		return new UnitOrderResult(
			(Actor)type.GetField("Actor")!.GetValue(obj)!,
			type.GetField("Order")!.GetValue(obj) as IOrderTargeter,
			(IIssueOrder)type.GetField("Trait")!.GetValue(obj)!);
	}

	private record UnitOrderResult(Actor Actor, IOrderTargeter? Order, IIssueOrder Trait)
	{
		public IOrderPreviewRender? OrderPreview => this.Trait as IOrderPreviewRender;

		public IEnumerable<IRenderable>? Render(WorldRenderer wr)
		{
			return this.OrderPreview?.Render(this.Actor, wr);
		}

		public IEnumerable<IRenderable>? RenderAboveShroud(WorldRenderer wr)
		{
			return this.OrderPreview?.RenderAboveShroud(this.Actor, wr);
		}

		public IEnumerable<IRenderable>? RenderAnnotations(WorldRenderer wr)
		{
			return this.OrderPreview?.RenderAnnotations(this.Actor, wr);
		}
	}
}
