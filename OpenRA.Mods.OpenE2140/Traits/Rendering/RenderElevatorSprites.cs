#region Copyright & License Information

/*
 * Copyright 2007-2023 The OpenE2140 Developers (see AUTHORS)
 * This file is part of OpenE2140, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */

#endregion

using System.Collections;
using System.Reflection;
using JetBrains.Annotations;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.OpenE2140.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Mods.OpenE2140.Traits.Rendering;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("Cutoff sprites before rendering, used for the elevator.")]
public class RenderElevatorSpritesInfo : RenderSpritesInfo
{
	public override object Create(ActorInitializer init)
	{
		return new RenderElevatorSprites(init, this);
	}
}

public class RenderElevatorSprites : RenderSprites
{
	public RenderElevatorSprites(ActorInitializer init, RenderSpritesInfo info)
		: base(init, info)
	{
	}

	public override IEnumerable<IRenderable> Render(Actor self, WorldRenderer worldRenderer)
	{
		// TODO Hack: this.Anims is private.
		if (typeof(RenderSprites).GetField("anims", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(this) is not IEnumerable anims)
			yield break;

		foreach (var animationWrapper in anims)
		{
			// TODO Hack: the whole class is a private class, so we need to access this via reflection...
			if (animationWrapper.GetType().GetProperty("IsVisible")?.GetValue(animationWrapper) is not true)
				continue;

			var paletteReferenceProperty = animationWrapper.GetType().GetProperty("PaletteReference");

			if (paletteReferenceProperty?.GetValue(animationWrapper) == null)
			{
				animationWrapper.GetType()
					.GetMethod("CachePalette")
					?.Invoke(
						animationWrapper,
						new object[] { worldRenderer, self.EffectiveOwner is { Disguised: true } ? self.EffectiveOwner.Owner : self.Owner }
					);
			}

			if (animationWrapper.GetType().GetField("Animation")?.GetValue(animationWrapper) is not AnimationWithOffset animation)
				continue;

			var renderables = animation.Render(self, paletteReferenceProperty?.GetValue(animationWrapper) as PaletteReference);

			if (animation is CutOffAnimationWithOffset elevatorAnimation)
				RenderElevatorSprites.PostProcess(renderables, elevatorAnimation.Bottom());

			foreach (var renderable in renderables)
				yield return renderable;
		}
	}

	public static void PostProcess(IEnumerable<IRenderable> renderables, int bottom)
	{
		foreach (var renderable in renderables.OfType<SpriteRenderable>())
		{
			// TODO Hack: SpriteRenderable.Sprite is private.
			var spriteField = renderable.GetType().GetField("sprite", BindingFlags.Instance | BindingFlags.NonPublic);

			if (spriteField?.GetValue(renderable) is not Sprite sprite)
				continue;

			spriteField.SetValue(
				renderable,
				new Sprite(
					sprite.Sheet,
					new Rectangle(sprite.Bounds.X, sprite.Bounds.Y, sprite.Bounds.Width, sprite.Bounds.Height + bottom),
					sprite.ZRamp,
					sprite.Offset + new float3(0, bottom / 2f, 0),
					sprite.Channel,
					sprite.BlendMode
				)
			);
		}
	}
}
