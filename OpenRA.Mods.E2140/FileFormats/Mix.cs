using System.Collections.Generic;
using System.IO;
using OpenRA.Primitives;

namespace OpenRA.Mods.E2140.FileFormats
{
	public class Mix
	{
		public readonly MixFrame[] Frames;
		public readonly Dictionary<int, MixPalette> Palettes = new Dictionary<int, MixPalette>();

		public Mix(Stream stream)
		{
			stream.ReadASCII(10); // "MIX FILE  "
			stream.ReadInt32(); // dataSize
			var frameOffsets = new int[stream.ReadInt32()];
			var frameOffset = stream.ReadInt32();
			var numPalettes = stream.ReadInt32();
			var firstPaletteId = stream.ReadInt32();
			stream.ReadInt32(); // palettesOffset

			stream.ReadASCII(5); // "ENTRY"

			for (var i = 0; i < frameOffsets.Length; i++)
				frameOffsets[i] = stream.ReadInt32();

			stream.ReadASCII(5); // " PAL "

			for (var i = 0; i < numPalettes; i++)
				Palettes.Add(firstPaletteId + i, new MixPalette(stream));

			Frames = new MixFrame[frameOffsets.Length];

			for (var i = 0; i < frameOffsets.Length; i++)
			{
				var frameStart = frameOffset + frameOffsets[i];
				var frameEnd = i + 1 < frameOffsets.Length ? frameOffset + frameOffsets[i + 1] : stream.Length;
				Frames[i] = new MixFrame(new SegmentStream(stream, frameStart, frameEnd - frameStart));
			}
		}
	}
}
