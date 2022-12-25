#region Copyright & License Information

/*
 * Copyright 2007-2022 The Earth 2140 Developers (see AUTHORS)
 * This file is part of OpenKrush, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */

#endregion

using OpenRA.Primitives;

namespace OpenRA.Mods.E2140.FileFormats;

public class Mix
{
	public readonly MixFrame[] Frames;
	public readonly Dictionary<int, MixPalette> Palettes = new();

	public Mix(Stream stream)
	{
		if (stream.ReadASCII(10) != "MIX FILE  ")
			throw new("Not a mix file!");

		var dataSize = stream.ReadInt32();
		var frameOffsets = new int[stream.ReadInt32()];
		var frameOffset = stream.ReadInt32();
		var numPalettes = stream.ReadInt32();
		var firstPaletteId = stream.ReadInt32();
		var palettesOffset = stream.ReadInt32();

		if (stream.ReadASCII(5) != "ENTRY")
			throw new("Broken mix file!");

		for (var i = 0; i < frameOffsets.Length; i++)
			frameOffsets[i] = stream.ReadInt32();

		if (stream.ReadASCII(5) != " PAL ")
			throw new("Broken mix file!");

		if (stream.Position != palettesOffset)
			throw new("Broken mix file!");

		for (var i = 0; i < numPalettes; i++)
			this.Palettes.Add(firstPaletteId + i, new(stream));

		if (stream.ReadASCII(5) != "DATA ")
			throw new("Broken mix file!");

		if (dataSize != stream.Length - stream.Position)
			throw new("Broken mix file!");

		this.Frames = new MixFrame[frameOffsets.Length];

		for (var i = 0; i < frameOffsets.Length; i++)
		{
			var frameStart = frameOffset + frameOffsets[i];
			var frameEnd = i + 1 < frameOffsets.Length ? frameOffset + frameOffsets[i + 1] : stream.Length;
			this.Frames[i] = new(new SegmentStream(stream, frameStart, frameEnd - frameStart));
		}
	}
}
