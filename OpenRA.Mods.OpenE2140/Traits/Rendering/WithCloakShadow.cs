﻿#region Copyright & License Information

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

[Desc("Trait that makes cloaked units with cloak shadow effect.")]
public class WithCloakShadowInfo : TraitInfo, Requires<CloakInfo>, Requires<RenderSpritesInfo>
{
	[Desc("Color to draw shadow.")]
	public readonly Color ShadowColor = Color.FromArgb(140, 0, 0, 0);

	[Desc($"Apply cloaked effect to render modifiers from specified traits. Key is trait name, value is alpha (which defaults to value from {nameof(ShadowColor)}).")]
	public readonly Dictionary<string, float?> ApplyToRenderModifierTraits = new Dictionary<string, float?>();

	[Desc("Render specified traits fully without applying shadow effect.")]
	public readonly string[] TraitsToFullyRender = new[] { "WithMuzzleOverlay" };  // TODO: PR for making it public

	[Desc("Render specified traits even when the actor is completely invisible (i.e. owner is not render player).")]
	public readonly string[] TraitsToRenderWhenInvisibile = new[] { "WithMuzzleOverlay" };  // TODO: PR for making it public

	[Desc("Render cloaked units with transparency effect instead of shadow effect.")]
	public readonly bool TransparentAppearance = false;

	[Desc($"Render specified sequences (from {nameof(RenderSprites)}) using custom shadow alpha value. Key is sequence name, value is alpha." +
		  "Default alpha is 1.0 (i.e. no cloak shadow effect)")]
	public readonly Dictionary<string, float?> OverrideShadowAlphaForSequences = new Dictionary<string, float?>();

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

		if (this.renderModifiers != null)
			renderables = this.ApplyRenderModifiersWithCloakShadow(self, wr, renderables);
		return renderables;
	}

	/// <summary>
	/// Custom method for determining visibility. It is necessary, because invisibility due to cloak has to be handles by this trait.
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
				var renderables = rsHelper.RenderModifiedAnimations(
					self, wr, rsHelper.GetVisibleAnimations(),
					(anim, renderables) =>
					{
						var alphaToApply = this.shadowAlpha;
						var currentSequence = anim.Animation.CurrentSequence?.Name;
						if (!string.IsNullOrEmpty(currentSequence) && this.info.OverrideShadowAlphaForSequences.TryGetValue(currentSequence, out var customAlpha))
							alphaToApply = customAlpha ?? 1.0f;

						return renderables.OfType<IModifyableRenderable>().Select(r => this.ApplyCloakShadow(r, alphaToApply));
					});

				foreach (var r in renderables)
				{
					yield return r;
				}
			}
			else if (this.info.TraitsToFullyRender.Contains(item.GetType().Name))
			{
				// render specified traits without any modification
				foreach (var r in item.Render(self, wr))
					yield return r;
			}
			else
			{
				// render remaining traits with cloak shadow effect
				foreach (var r in item.Render(self, wr).OfType<IModifyableRenderable>())
					yield return this.ApplyCloakShadow(r, this.shadowAlpha);
			}
		}
	}

	private IEnumerable<IRenderable> ApplyRenderModifiersWithCloakShadow(Actor self, WorldRenderer wr, IEnumerable<IRenderable> renderables)
	{
		if (this.renderModifiers == null)
		{
			return renderables;
		}

		// This algorithm:
		// - iterates each IRenderModifier
		// - lets them modify renderables
		// - modified renderables from specified traits are processed:
		//		- cloak shadow effect is applied new renderables, existing are reused as-is, removed are thrown away
		// - modified renderables from rest of traits are reused as-is
		// - next iteration uses this new list of renderables
		// - returns final list of transformed renderables

		var currentRenderables = renderables.ToHashSet();
		var nextRenderables = new HashSet<IRenderable>();

		foreach (var item in this.renderModifiers)
		{
			var modifiedRenderables = item.ModifyRender(self, wr, currentRenderables).OfType<IModifyableRenderable>();

			if (this.info.ApplyToRenderModifierTraits.TryGetValue(item.GetType().Name, out var customAlpha))
			{
				// apply cloak shadow effect to specified traits
				foreach (var r in modifiedRenderables)
				{
					if (!currentRenderables.Contains(r))
					{
						// don't render renderables that have alpha = 0 at all
						if (customAlpha == 0.0f)
							continue;

						// this IRenderable is new, apply cloak shadow effect
						nextRenderables.Add(this.ApplyCloakShadow(r, customAlpha ?? this.shadowAlpha));
					}
					else
					{
						// this IRenderable is old, don't apply cloak shadow effect again
						nextRenderables.Add(r);
					}
				}
			}
			else
			{
				// render remaining traits without adding any modification cloak shadow effect
				foreach (var r in modifiedRenderables)
					nextRenderables.Add(r);
			}

			(nextRenderables, currentRenderables) = (currentRenderables, nextRenderables);
			nextRenderables.Clear();
		}

		return currentRenderables;
	}

	private static IEnumerable<IRenderable> GetRenderables(Actor self, WorldRenderer wr, IEnumerable<IRender> renderTraits)
	{
		foreach (var trait in renderTraits)
			foreach (var r in trait.Render(self, wr))
				yield return r;
	}

	private IRenderable ApplyCloakShadow(IModifyableRenderable r, float shadowAlpha)
	{
		if (!this.info.TransparentAppearance)
			r = r.WithTint(this.shadowColor, r.TintModifiers | TintModifiers.ReplaceColor);
		return r
			.WithAlpha(shadowAlpha)
			.AsDecoration();
	}

	IEnumerable<Rectangle> IRenderModifier.ModifyScreenBounds(Actor self, WorldRenderer wr, IEnumerable<Rectangle> bounds)
	{
		return bounds;
	}
}
