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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Mods.OpenE2140.Assets.VirtualAssets;

[UsedImplicitly]
public class VirtualSpriteLoader : ISpriteLoader
{
	private class SpriteFrame : ISpriteFrame
	{
		public SpriteFrameType Type { get; }
		public Size Size { get; }
		public Size FrameSize { get; }
		public float2 Offset { get; }
		public byte[] Data { get; }
		public bool DisableExportPadding => true;

		public SpriteFrame(SpriteFrameType type, Size size, float2 offset, byte[] pixels)
		{
			this.Type = type;
			this.Size = size;
			this.FrameSize = size;
			this.Offset = offset;
			this.Data = pixels;
		}
	}

	public bool TryParseSprite(Stream stream, string filename, [NotNullWhen(true)] out ISpriteFrame[]? frames, out TypeDictionary? metadata)
	{
		var identifier = stream.ReadASCII(VirtualAssetsBuilder.Identifier.Length);

		if (identifier != VirtualAssetsBuilder.Identifier)
		{
			stream.Position -= 4;

			frames = null;
			metadata = null;

			return false;
		}

		frames = VirtualAssetsBuilder.Cache[stream.ReadASCII(stream.ReadInt32())]
			.Select(
				frame => new SpriteFrame(
					SpriteFrameType.Rgba32,
					frame.Bounds.Size,
					new float2(frame.Bounds.X + frame.Bounds.Width / 2f, frame.Bounds.Y + frame.Bounds.Height / 2f),
					frame.Pixels
				)
			)
			.Cast<ISpriteFrame>()
			.ToArray();

		metadata = null;

		return true;
	}
}
