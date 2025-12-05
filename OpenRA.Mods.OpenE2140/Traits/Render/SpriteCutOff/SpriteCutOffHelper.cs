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
using OpenRA.Mods.OpenE2140.Graphics;
using OpenRA.Mods.OpenE2140.Helpers.Reflection;
using OpenRA.Primitives;

namespace OpenRA.Mods.OpenE2140.Traits.Render;

public static class SpriteCutOffHelper
{
	private static readonly TypeFieldHelper<Sprite> SpriteFieldHelper = ReflectionHelper.GetTypeFieldHelper<Sprite>(typeof(SpriteRenderable), "sprite");

	public static void ApplyCutOff(IEnumerable<IRenderable> renderables, Func<IRenderable, int> cutOffFunc, CutOffDirection cutOffDirection)
	{
		foreach (var renderable in renderables.OfType<SpriteRenderable>())
		{
			var sprite = SpriteFieldHelper.GetValue(renderable);

			if (sprite == null)
				continue;

			var cutOff = cutOffFunc(renderable);

			var bounds = sprite.Bounds;
			var spriteOffset = sprite.Offset;

			if (cutOffDirection == CutOffDirection.Bottom)
			{
				var height = sprite.Bounds.Height;
				var offset = sprite.Offset.Y;
				var current = renderable.Offset.Y - renderable.Offset.Z + height * 16 / 2;

				if (current > cutOff)
				{
					height = Math.Max(height - (current - cutOff) / 16, 0);
					offset -= (sprite.Bounds.Height - height) / 2f;
				}

				bounds = new Rectangle(sprite.Bounds.X, sprite.Bounds.Y, sprite.Bounds.Width, height);
				spriteOffset = new float3(sprite.Offset.X, offset, sprite.Offset.Z);
			}
			else if (cutOffDirection == CutOffDirection.Top)
			{
				cutOff = cutOffFunc(renderable);

				var height = sprite.Bounds.Height;
				var offset = sprite.Offset.Y;
				var current = renderable.Offset.Y - renderable.Offset.Z;

				if (current < cutOff)
				{
					height = Math.Max(height - (cutOff - current) / 16, 0);
					offset -= (height - sprite.Bounds.Height) / 2f;
				}

				bounds = new Rectangle(sprite.Bounds.X, sprite.Bounds.Y + sprite.Bounds.Height - height, sprite.Bounds.Width, height);
				spriteOffset = new float3(sprite.Offset.X, offset + sprite.Offset.Y, sprite.Offset.Z);
			}

			SpriteFieldHelper.SetValue(
				renderable,
				new Sprite(
					sprite.Sheet,
					bounds,
					sprite.ZRamp,
					spriteOffset,
					sprite.Channel,
					sprite.BlendMode
				)
			);
		}
	}
}
