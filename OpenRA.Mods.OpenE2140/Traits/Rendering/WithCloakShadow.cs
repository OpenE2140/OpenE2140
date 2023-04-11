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
	public readonly string[] TraitsToFullyRender = new[] { "WithMuzzleOverlay" };  // TODO: PR for making it public

	[Desc("Render specified traits even when the actor is completely invisible (i.e. owner is not render player).")]
	public readonly string[] TraitsToRenderWhenInvisibile = new[] { "WithMuzzleOverlay" };  // TODO: PR for making it public

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
	private readonly IDefaultVisibility defaultVisibility;
	private readonly IVisibilityModifier[] visibilityModifiers;
	private IRenderModifier[]? renderModifiers;
	private IRender[]? renderTraitsForInvisibleActors;

	public WithCloakShadow(Actor self, WithCloakShadowInfo info)
	{
		this.info = info;
		this.cloak = self.Trait<Cloak>();
		this.shadowColor = new float3(info.ShadowColor.R, info.ShadowColor.G, info.ShadowColor.B) / 255f;
		this.shadowAlpha = info.ShadowColor.A / 255f;

		this.reflectionHelpers = self.TraitsImplementing<RenderSprites>()
			.ToDictionary(rs => rs, rs => new RenderSpritesReflectionHelper(rs));
		this.defaultVisibility = self.TraitsImplementing<IDefaultVisibility>().Last();
		this.visibilityModifiers = self.TraitsImplementing<IVisibilityModifier>()
			.Where(m => m is not Cloak)
			.ToArray();
	}

	void INotifyCreated.Created(Actor self)
	{
		this.renderModifiers = self.TraitsImplementing<IRenderModifier>()
			.Where(rm => rm is not WithCloakShadow && rm is not Cloak)
			.ToArray();
		this.renderTraitsForInvisibleActors = self.TraitsImplementing<IRender>()
			.Where(r => this.info.TraitsToRenderWhenInvisibile.Contains(r.GetType().Name))
			.ToArray();
	}

	IEnumerable<IRenderable> IRenderModifier.ModifyRender(Actor self, WorldRenderer wr, IEnumerable<IRenderable> renderables)
	{
		// Actor is obscured by fog/shroud or not cloaked at all, ignore these and don't modify renderables.
		if (this.FogObscures(self) || self.World.ShroudObscures(self.CenterPosition) || !this.cloak.Cloaked)
			return renderables;

		// Actor is cloaked, but visible to render player. Apply cloak shadow effect to traits.
		if (this.cloak.IsVisible(self, self.World.RenderPlayer))
			renderables = GetRenderablesWithCloakShadow(self, wr);
		else if (this.renderTraitsForInvisibleActors != null)
		{
			// Actor is cloaked, but invisible to render player. Fully render renderables only from specified traits.
			renderables = GetRenderables(self, wr, this.renderTraitsForInvisibleActors);
		}

		// Use same pipeline as in Actor.Render here:
		// - get renderables from IRender traits
		// - call IModifyRender traits to modify renderables

		if (this.renderModifiers != null)
		{
			foreach (var modifier in this.renderModifiers)
				renderables = modifier.ModifyRender(self, wr, renderables);
		}
		return renderables;
	}

	/// <summary>
	/// Custom method for determining visibility is needed, because invisibility due to cloak has to be handles by this trait.
	/// </summary>
	private bool FogObscures(Actor self)
	{
		foreach (var visibilityModifier in visibilityModifiers)
			if (!visibilityModifier.IsVisible(self, self.World.RenderPlayer))
				return true;

		return !defaultVisibility.IsVisible(self, self.World.RenderPlayer);
	}

	private IEnumerable<IRenderable> GetRenderablesWithCloakShadow(Actor self, WorldRenderer wr)
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
			else if (this.info.TraitsToFullyRender.Contains(item.GetType().Name))
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

	private static IEnumerable<IRenderable> GetRenderables(Actor self, WorldRenderer wr, IEnumerable<IRender> renderTraits)
	{
		foreach (var trait in renderTraits)
			foreach (var r in trait.Render(self, wr))
				yield return r;
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
