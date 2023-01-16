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

using OpenRA.Primitives;

namespace OpenRA.Mods.OpenE2140.Assets.FileFormats;

public class Mix
{
	public readonly MixFrame[] Frames;
	public readonly Dictionary<uint, MixPalette> Palettes = new Dictionary<uint, MixPalette>();

	public Mix(Stream stream)
	{
		if (stream.ReadASCII(10) != "MIX FILE  ")
			throw new Exception("Not a mix file!");

		var dataSize = stream.ReadUInt32();
		var frameOffsets = new uint[stream.ReadUInt32()];
		var frameOffset = stream.ReadUInt32();
		var numPalettes = stream.ReadUInt32();
		var firstPaletteId = stream.ReadUInt32();
		var palettesOffset = stream.ReadUInt32();

		if (stream.ReadASCII(5) != "ENTRY")
			throw new Exception("Broken mix file!");

		for (var i = 0; i < frameOffsets.Length; i++)
			frameOffsets[i] = stream.ReadUInt32();

		if (stream.ReadASCII(5) != " PAL ")
			throw new Exception("Broken mix file!");

		if (stream.Position != palettesOffset)
			throw new Exception("Broken mix file!");

		for (var i = 0u; i < numPalettes; i++)
			this.Palettes.Add(firstPaletteId + i, new MixPalette(stream));

		if (stream.ReadASCII(5) != "DATA ")
			throw new Exception("Broken mix file!");

		if (dataSize != stream.Length - stream.Position)
			throw new Exception("Broken mix file!");

		this.Frames = new MixFrame[frameOffsets.Length];

		for (var i = 0; i < frameOffsets.Length; i++)
		{
			var frameStart = frameOffset + frameOffsets[i];
			var frameEnd = i + 1 < frameOffsets.Length ? frameOffset + frameOffsets[i + 1] : stream.Length;
			this.Frames[i] = new MixFrame(new SegmentStream(stream, frameStart, frameEnd - frameStart));
		}
	}
}
