﻿using System.Collections;
using System.Reflection;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.OpenE2140.Helpers.Reflection;

namespace OpenRA.Mods.OpenE2140.Traits.Rendering
{
	public class RenderSpritesReflectionHelper
	{
		private readonly RenderSprites rs;
		private readonly ObjectFieldHelper<IList> animField;
		private readonly AnimWrapperHelper animWrapperHelper;

		public RenderSpritesReflectionHelper(RenderSprites rs)
		{
			this.rs = rs ?? throw new ArgumentNullException(nameof(rs));

			this.animField = ReflectionHelper.GetFieldHelper(rs, animField, "anims");
			this.animWrapperHelper = new AnimWrapperHelper();
		}

		public IEnumerable<AnimationWithOffset> GetVisibleAnimations()
		{
			var anims = this.animField.Value!.Cast<object>();

			return anims.Where(anim => this.animWrapperHelper.IsVisible(anim))
				.Select(s => this.animWrapperHelper.GetAnimation(s));
		}

		public IEnumerable<IRenderable> RenderAnimations(Actor self, WorldRenderer worldRenderer, IEnumerable<AnimationWithOffset> animations,
			Action<AnimationWithOffset, IEnumerable<IRenderable>>? renderablePostProcess = null)
		{
			var animLookup = this.animField.Value!.Cast<object>().ToDictionary(wrapper => this.animWrapperHelper.GetAnimation(wrapper));

			foreach (var animation in animations)
			{
				if (!animLookup.TryGetValue(animation, out var animWrapper) ||
					this.animWrapperHelper.GetAnimation(animWrapper) != animation)
				{
					throw new InvalidOperationException($"Unknown animation passed to {nameof(RenderSpritesReflectionHelper)}");
				}

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

		private class AnimWrapperHelper
		{
			private readonly TypePropertyHelper<bool> isVisible;
			private readonly TypeFieldHelper<AnimationWithOffset> animation;
			private readonly TypePropertyHelper<PaletteReference> paletteReference;
			private readonly MethodInfo cachePalette;

			public AnimWrapperHelper()
			{
				var animWrapperType = typeof(RenderSprites).GetNestedType("AnimationWrapper", BindingFlags.Instance | BindingFlags.NonPublic)!;
				this.isVisible = new TypePropertyHelper<bool>(animWrapperType.GetProperty("IsVisible", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!);
				this.animation = new TypeFieldHelper<AnimationWithOffset>(animWrapperType.GetField("Animation", BindingFlags.Instance | BindingFlags.Public)!);
				this.paletteReference = new TypePropertyHelper<PaletteReference>(animWrapperType.GetProperty("PaletteReference", BindingFlags.Instance | BindingFlags.Public)!);
				this.cachePalette = animWrapperType.GetMethod("CachePalette", BindingFlags.Instance | BindingFlags.Public)!;
			}

			public bool IsVisible(object animWrapper) => this.isVisible.GetValue(animWrapper);

			public AnimationWithOffset GetAnimation(object animWrapper) => this.animation.GetValue(animWrapper)!;

			public PaletteReference? GetPaletteReference(object animWrapper) => this.paletteReference.GetValue(animWrapper);

			public void CachePalette(object animWrapper, WorldRenderer worldRenderer, Player owner)
			{
				this.cachePalette?.Invoke(
						animWrapper,
						new object[] { worldRenderer, owner }
					);
			}
		}
	}
}