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
using OpenRA.Mods.E2140.FileFormats;
using OpenRA.Primitives;

namespace OpenRA.Mods.E2140.SpriteLoaders;

public class PcxSpriteFrame : ISpriteFrame
{
	public SpriteFrameType Type => SpriteFrameType.Rgba32;
	public Size Size { get; }
	public Size FrameSize { get; }
	public float2 Offset { get; }
	public byte[] Data { get; }
	public bool DisableExportPadding => true;

	public PcxSpriteFrame(Size size, byte[] pixels)
	{
		this.Size = size;
		this.FrameSize = size;
		this.Offset = new float2(0, 0);
		this.Data = pixels;
	}
}

[UsedImplicitly]
public class PcxSpriteLoader : ISpriteLoader
{
	public bool TryParseSprite(Stream stream, string filename, [NotNullWhen(true)] out ISpriteFrame[]? frames, out TypeDictionary? metadata)
	{
		var start = stream.Position;
		var identifier = stream.ReadByte();
		stream.Position = start;

		frames = null;
		metadata = null;

		if (identifier != 0x0a)
			return false;

		var pcx = new Pcx(stream);
		var size = new Size(pcx.Width, pcx.Height);

		frames = new[] { new PcxSpriteFrame(size, pcx.Pixels.SelectMany(color => new[] { color.R, color.G, color.B, color.A }).ToArray()) };

		return true;
	}
}
