using System;
using System.IO;
using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Mods.E2140.SpriteLoaders
{
	class MixSpriteFrame : ISpriteFrame
	{
		public Size Size { get; private set; }
		public Size FrameSize { get { return Size; } }
		public float2 Offset { get { return float2.Zero; } }
		public byte[] Data { get; set; }
		public bool DisableExportPadding { get { return false; } }

		public SpriteFrameType Type => SpriteFrameType.Indexed8;

		public MixSpriteFrame(Stream stream)
		{
			Size = new Size(stream.ReadUInt16(), stream.ReadUInt16());
			var type = stream.ReadUInt8();
			/*var paletteId = */
			stream.ReadUInt8();

			Data = new byte[Size.Width * Size.Height];

			switch (type)
			{
				case 2:
					// Nothing to do here till 32bpp images are supported!
					break;
				case 9:
					/*var widthCopy = */
					stream.ReadInt32();
					/*var heightCopy = */
					stream.ReadInt32();
					var dataSize = stream.ReadInt32();

					var numScanlines = stream.ReadInt32();
					var numPatterns = stream.ReadInt32();
					/*var scanLinesOffset = */
					stream.ReadInt32();
					/*var dataOffsetsOffset = */
					stream.ReadInt32();
					/*var patternsOffset = */
					stream.ReadInt32();

					var compressedImageDataOffset = stream.ReadInt32();

					var scanlines = new int[numScanlines];
					for (var i = 0; i < numScanlines; i++)
						scanlines[i] = stream.ReadUInt16();

					var dataOffsets = new int[numScanlines];
					for (var i = 0; i < numScanlines; i++)
						dataOffsets[i] = stream.ReadUInt16();

					var patterns = stream.ReadBytes(numPatterns);
					var data = new SegmentStream(stream, compressedImageDataOffset + 6, dataSize);

					var writePosition = 0;
					for (var i = 0; i < Size.Height; i++)
					{
						data.Position = dataOffsets[i];

						if (scanlines[i] == scanlines[i + 1])
							writePosition += Size.Width;
						else
						{
							for (var j = scanlines[i]; j < scanlines[i + 1]; j += 2)
							{
								writePosition += patterns[j];
								var pixels = patterns[j + 1];
								Array.Copy(data.ReadBytes(pixels), 0, Data, writePosition, pixels);
								writePosition += pixels;
							}

							if (writePosition % Size.Width != 0)
								writePosition += Size.Width - writePosition % Size.Width;
						}
					}

					break;
				default:
					Log.Write("debug", "Unknown MixSprite type " + type);
					break;
			}
		}
	}

	public class MixSpriteLoader : ISpriteLoader
	{
		public bool TryParseSprite(Stream stream, string filename, out ISpriteFrame[] frames, out TypeDictionary metadata)
		{
			var start = stream.Position;
			var identifier = stream.ReadASCII(10);

			if (identifier != "MIX FILE  ")
			{
				stream.Position = start;
				frames = Array.Empty<ISpriteFrame>();
				metadata = null;
				return false;
			}

			var dataSize = stream.ReadInt32();
			frames = new ISpriteFrame[stream.ReadInt32()];
			var imagesOffset = stream.ReadInt32();
			/*var numPalettes = */
			stream.ReadInt32();
			/*var firstPaletteId = */
			stream.ReadInt32();
			/*var paletteOffset = */
			stream.ReadInt32();

			/*var entry = */
			stream.ReadASCII(5); // "ENTRY"

			for (var i = 0; i < frames.Length; i++)
			{
				var imageOffset = stream.ReadInt32();
				frames[i] = new MixSpriteFrame(SegmentStream.CreateWithoutOwningStream(stream, imagesOffset + imageOffset, dataSize - imageOffset));
			}

			// /*var pal = */stream.ReadASCII(5); // " PAL "

			// for (var i = 0; i < numPalettes; i++)
			// {
			//     Palette palette;
			// }
			metadata = null;
			return true;
		}
	}
}
