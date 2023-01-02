#region Copyright & License Information

/*
 * Copyright 2007-2023 The Earth 2140 Developers (see AUTHORS)
 * This file is part of Earth 2140, which is free software. It is made
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

public class FlcSpriteFrame : ISpriteFrame
{
	public SpriteFrameType Type => SpriteFrameType.Rgba32;
	public Size Size { get; }
	public Size FrameSize { get; }
	public float2 Offset { get; }
	public byte[] Data { get; }
	public bool DisableExportPadding => true;

	public FlcSpriteFrame(Size size, byte[] pixels)
	{
		this.Size = size;
		this.FrameSize = size;
		this.Offset = new float2(0, 0);
		this.Data = pixels;
	}
}

[UsedImplicitly]
public class FlcSpriteLoader : ISpriteLoader
{
	public bool TryParseSprite(Stream stream, string filename, [NotNullWhen(true)] out ISpriteFrame[]? frames, out TypeDictionary? metadata)
	{
		frames = null;
		metadata = null;

		if (!filename.EndsWith(".flc", StringComparison.OrdinalIgnoreCase))
			return false;

		var flc = new Flc(stream);

		if (flc.Frames.Length == 0)
			return false;

		var size = new Size(flc.Width, flc.Height);

		frames = flc.Frames.Select(frame => new FlcSpriteFrame(size, frame.Pixels.SelectMany(color => new[] { color.R, color.G, color.B, color.A }).ToArray()))
			.Cast<ISpriteFrame>()
			.ToArray();

		return true;
	}
}
