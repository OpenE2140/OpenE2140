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
using OpenRA.Mods.OpenE2140.Helpers.Reflection;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Rendering;

public class WithCloakShadowInfo : TraitInfo, Requires<CloakInfo>, Requires<RenderSpritesInfo>
{
	[Desc("Color to draw shadow.")]
	public readonly Color ShadowColor = Color.FromArgb(140, 0, 0, 0);

	[Desc("Render specified traits fully without applying shadow effect.")]
	public readonly string[] FullyRenderTraits = new[] { "WithMuzzleOverlay" };  // TODO: PR for making it public

	public override object Create(ActorInitializer init)
	{
		return new WithCloakShadow(init.Self, this);
	}
}

public class WithCloakShadow : IRenderModifier, INotifyCreated
{
	private readonly WithCloakShadowInfo info;
	private readonly Cloak cloak;
	private readonly float3 shadowColor;
	private readonly float shadowAlpha;
	private readonly Dictionary<RenderSprites, RenderSpritesReflectionHelper> reflectionHelpers;
	private IEnumerable<IRenderModifier>? renderModifiers;

	public WithCloakShadow(Actor self, WithCloakShadowInfo info)
	{
		this.info = info;
		this.cloak = self.Trait<Cloak>();
		this.shadowColor = new float3(info.ShadowColor.R, info.ShadowColor.G, info.ShadowColor.B) / 255f;
		this.shadowAlpha = info.ShadowColor.A / 255f;

		this.reflectionHelpers = self.TraitsImplementing<RenderSprites>()
			.ToDictionary(rs => rs, rs => new RenderSpritesReflectionHelper(rs));
	}

	void INotifyCreated.Created(Actor self)
	{
		this.renderModifiers = self.TraitsImplementing<IRenderModifier>();
	}

	IEnumerable<IRenderable> IRenderModifier.ModifyRender(Actor self, WorldRenderer wr, IEnumerable<IRenderable> renderables)
	{
		if (self.World.FogObscures(self) || self.World.ShroudObscures(self.CenterPosition))
			return renderables;

		if (!this.cloak.Cloaked || !this.cloak.IsVisible(self, self.World.RenderPlayer))
			return renderables;

		// Use same pipeline as in Actor.Render here:
		// - get IEnumerable<IRenderable> from IRender traits
		// - call IModifyRender traits with list of IRenderable objects
		renderables = GetRenderables(self, wr);

		if (this.renderModifiers != null)
		{
			foreach (var modifier in this.renderModifiers.Where(rm => rm is not WithCloakShadow))
				renderables = modifier.ModifyRender(self, wr, renderables);
		}
		return renderables;
	}

	private IEnumerable<IRenderable> GetRenderables(Actor self, WorldRenderer wr)
	{
		foreach (var item in self.TraitsImplementing<IRender>())
		{
			// render RenderSprites traits with cloak shadow effect
			if (item is RenderSprites rs && this.reflectionHelpers.TryGetValue(rs, out var rsHelper))
			{
				foreach (var r in rsHelper.RenderAnimations(self, wr, rsHelper.GetVisibleAnimations()).OfType<IModifyableRenderable>())
				{
					yield return this.ApplyCloakShadow(r);
				}
			}
			else if (this.info.FullyRenderTraits.Contains(item.GetType().Name))
			{
				// render specified traits without cloak shadow effect
				foreach (var r in item.Render(self, wr))
					yield return r;
			}
			else
			{
				// render remaining traits with cloak shadow effect
				// TODO: do we need to handle IRenderable that is not IModifyableRenderable?
				foreach (var r in item.Render(self, wr).OfType<IModifyableRenderable>())
					yield return this.ApplyCloakShadow(r);
			}
		}
	}

	private IRenderable ApplyCloakShadow(IModifyableRenderable r)
	{
		return r.WithTint(this.shadowColor, r.TintModifiers | TintModifiers.ReplaceColor)
			.WithAlpha(this.shadowAlpha)
			.AsDecoration();
	}

	IEnumerable<Rectangle> IRenderModifier.ModifyScreenBounds(Actor self, WorldRenderer wr, IEnumerable<Rectangle> bounds)
	{
		return bounds;
	}
}
