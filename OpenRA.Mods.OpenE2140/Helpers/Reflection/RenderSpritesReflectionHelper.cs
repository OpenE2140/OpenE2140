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

using System.Collections;
using System.Reflection;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits.Render;

namespace OpenRA.Mods.OpenE2140.Helpers.Reflection;

public class RenderSpritesReflectionHelper
{
	private readonly ObjectFieldHelper<IList> animField;
	private readonly AnimWrapperHelper animWrapperHelper;

	public RenderSpritesReflectionHelper(RenderSprites rs)
	{
		this.animField = ReflectionHelper.GetFieldHelper(rs, this.animField, "anims");
		this.animWrapperHelper = new AnimWrapperHelper();
	}

	public IEnumerable<AnimationWithOffset> GetVisibleAnimations()
	{
		var anims = this.animField.Value!.Cast<object>();

		return anims.Where(anim => this.animWrapperHelper.IsVisible(anim)).Select(s => this.animWrapperHelper.GetAnimation(s));
	}

	public IEnumerable<IRenderable> RenderAnimations(
		Actor self,
		WorldRenderer worldRenderer,
		IEnumerable<AnimationWithOffset> animations,
		Action<AnimationWithOffset, IEnumerable<IRenderable>>? renderablePostProcess = null
	)
	{
		var animLookup = this.animField.Value!.Cast<object>().ToDictionary(wrapper => this.animWrapperHelper.GetAnimation(wrapper));

		foreach (var animation in animations)
		{
			if (!animLookup.TryGetValue(animation, out var animWrapper) || this.animWrapperHelper.GetAnimation(animWrapper) != animation)
				throw new InvalidOperationException($"Unknown animation passed to {nameof(RenderSpritesReflectionHelper)}");

			var paletteReference = this.animWrapperHelper.GetPaletteReference(animWrapper);

			if (paletteReference == null)
			{
				var owner = self.EffectiveOwner is { Disguised: true } ? self.EffectiveOwner.Owner : self.Owner;
				this.animWrapperHelper.CachePalette(animWrapper, worldRenderer, owner);
			}

			var renderables = animation.Render(self, this.animWrapperHelper.GetPaletteReference(animWrapper));
			renderablePostProcess?.Invoke(animation, renderables);

			foreach (var renderable in renderables)
				yield return renderable;
		}
	}

	public IEnumerable<IRenderable> RenderModifiedAnimations(
		Actor self,
		WorldRenderer worldRenderer,
		IEnumerable<AnimationWithOffset> animations,
		Func<AnimationWithOffset, IEnumerable<IRenderable>, IEnumerable<IRenderable>>? renderableProcess = null
	)
	{
		var animLookup = this.animField.Value!.Cast<object>().ToDictionary(wrapper => this.animWrapperHelper.GetAnimation(wrapper));

		foreach (var animation in animations)
		{
			if (!animLookup.TryGetValue(animation, out var animWrapper) || this.animWrapperHelper.GetAnimation(animWrapper) != animation)
				throw new InvalidOperationException($"Unknown animation passed to {nameof(RenderSpritesReflectionHelper)}");

			var paletteReference = this.animWrapperHelper.GetPaletteReference(animWrapper);

			if (paletteReference == null)
			{
				var owner = self.EffectiveOwner is { Disguised: true } ? self.EffectiveOwner.Owner : self.Owner;
				this.animWrapperHelper.CachePalette(animWrapper, worldRenderer, owner);
			}

			IEnumerable<IRenderable> renderables = animation.Render(self, this.animWrapperHelper.GetPaletteReference(animWrapper));

			if (renderableProcess != null)
				renderables = renderableProcess.Invoke(animation, renderables);

			foreach (var renderable in renderables)
				yield return renderable;
		}
	}

	private class AnimWrapperHelper
	{
		private readonly TypePropertyHelper<bool> isVisible;
		private readonly TypeFieldHelper<AnimationWithOffset> animation;
		private readonly TypePropertyHelper<PaletteReference> paletteReference;
		private readonly MethodInfo cachePalette;

		public AnimWrapperHelper()
		{
			var animWrapperType = typeof(RenderSprites).GetNestedType("AnimationWrapper", BindingFlags.Instance | BindingFlags.NonPublic)!;

			this.isVisible = new TypePropertyHelper<bool>(
				animWrapperType.GetProperty("IsVisible", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
			);

			this.animation = new TypeFieldHelper<AnimationWithOffset>(animWrapperType.GetField("Animation", BindingFlags.Instance | BindingFlags.Public)!);

			this.paletteReference =
				new TypePropertyHelper<PaletteReference>(animWrapperType.GetProperty("PaletteReference", BindingFlags.Instance | BindingFlags.Public)!);

			this.cachePalette = animWrapperType.GetMethod("CachePalette", BindingFlags.Instance | BindingFlags.Public)!;
		}

		public bool IsVisible(object animWrapper)
		{
			return this.isVisible.GetValue(animWrapper);
		}

		public AnimationWithOffset GetAnimation(object animWrapper)
		{
			return this.animation.GetValue(animWrapper)!;
		}

		public PaletteReference? GetPaletteReference(object animWrapper)
		{
			return this.paletteReference.GetValue(animWrapper);
		}

		public void CachePalette(object animWrapper, WorldRenderer worldRenderer, Player owner)
		{
			this.cachePalette.Invoke(animWrapper, new object[] { worldRenderer, owner });
		}
	}
}
