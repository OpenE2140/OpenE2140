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
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.OpenE2140.Activites.Move;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Mods.OpenE2140.Graphics;
using OpenRA.Mods.OpenE2140.Helpers.Reflection;
using OpenRA.Mods.OpenE2140.Traits.Production;
using OpenRA.Primitives;

namespace OpenRA.Mods.OpenE2140.Traits.Rendering.SpriteCutOff;

[Desc($"Custom version of {nameof(RenderSprites)}, which cuts off all sprites of the actor when leaving production actor.",
	$"Requires {nameof(ActorProducerInit)} present in {nameof(ActorInitializer)}'s {nameof(TypeDictionary)}. " +
	$"{nameof(MarkActorProducer)} trait adds this actor init object, so attach it to the production actor.")]
public class ProductionExitRenderSpritesInfo : RenderSpritesInfo
{
	[Desc("Apply this offset to cut-off row (in pixels). Use this to further tweak the cut-off point.")]
	public readonly int OffsetCutOff;

	public override object Create(ActorInitializer init)
	{
		return new ProductionExitRenderSprites(init, this);
	}
}

public class ProductionExitRenderSprites : RenderSprites
{
	private readonly RenderSpritesReflectionHelper reflectionHelper;
	private readonly Lazy<Actor>? producer;

	private Actor? Producer => this.producer?.Value;

	public new ProductionExitRenderSpritesInfo Info { get; }

	public ProductionExitRenderSprites(ActorInitializer init, ProductionExitRenderSpritesInfo info)
		: base(init, info)
	{
		this.Info = info;

		this.producer = init.GetOrDefault<ActorProducerInit>()?.Value?.Actor(init.Self.World);
		this.reflectionHelper = new RenderSpritesReflectionHelper(this);
	}

	public override IEnumerable<IRenderable> Render(Actor self, WorldRenderer worldRenderer)
	{
		if (this.Producer == null || !this.ShouldCutOff(self))
			return base.Render(self, worldRenderer);

		return this.RenderCutOffSprites(self, worldRenderer);
	}

	private bool ShouldCutOff(Actor self)
	{
		if (this.Producer == null)
			return false;

		// Don't crop sprites, if the actor has already fully left the production actor
		if (self.CurrentActivity is not (Mobile.LeaveProductionActivity or ProductionExitMove or Mobile.ReturnToCellActivity))
			return false;

		// Stop cropping sprites, when produced actor reaches certain distance from cut off point.
		return (GetCutOffPoint(this.Producer) - self.CenterPosition).ToWDist() <= WDist.FromCells(1);
	}

	private static WPos GetCutOffPoint(Actor producer)
	{
		return producer.CenterPosition + (producer.Exits()?.FirstOrDefault()?.Info?.SpawnOffset ?? WVec.Zero);
	}

	private IEnumerable<IRenderable> RenderCutOffSprites(Actor self, WorldRenderer worldRenderer)
	{
		var cutOffPoint = GetCutOffPoint(this.Producer!);
		var cellTopEdge = worldRenderer.ScreenPxPosition(cutOffPoint);

		return this.reflectionHelper.RenderAnimations(
			self,
			worldRenderer,
			this.reflectionHelper.GetVisibleAnimations(),
			(anim, renderables) =>
			{
				SpriteCutOffHelper.ApplyCutOff(
					renderables,
					r =>
					{
						if (r is not SpriteRenderable spriteRenderable)
							return 0;

						var renderBounds = spriteRenderable.ScreenBounds(worldRenderer);

						var cutOffPos = worldRenderer.ProjectedPosition(cellTopEdge - renderBounds.Location);

						if (spriteRenderable.Offset != WVec.Zero)
							cutOffPos -= spriteRenderable.Pos - spriteRenderable.Offset - cutOffPoint;

						return cutOffPos.Y + this.Info.OffsetCutOff;
					},
					CutOffDirection.Top);
			}
		);
	}
}
