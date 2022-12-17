using System;
using OpenRA.Primitives;

namespace OpenRA.Mods.E2140.FileFormats
{
	public class MixFrame
	{
		public readonly int Width;
		public readonly int Height;
		public readonly int Palette;
		public readonly byte[] Pixels;
		public readonly bool Is32bpp;

		public MixFrame(SegmentStream stream)
		{
			Width = stream.ReadUInt16();
			Height = stream.ReadUInt16();
			var type = stream.ReadUInt8();
			Palette = stream.ReadUInt8();

			Pixels = new byte[Width * Height * (type == 2 ? 4 : 1)];

			switch (type)
			{
				case 2:
					Is32bpp = true;

					for (var i = 0; i < Pixels.Length; i += 4)
					{
						var color16 = stream.ReadUInt16(); // RRRRRGGGGGGBBBBB
						Pixels[i + 0] = (byte)((color16 & 0xf800) >> 8);
						Pixels[i + 1] = (byte)((color16 & 0x07e0) >> 3);
						Pixels[i + 2] = (byte)((color16 & 0x001f) << 3);
						Pixels[i + 3] = 0xff;
					}

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

					for (var i = 0; i < Height; i++)
					{
						data.Position = dataOffsets[i];

						if (scanlines[i] == scanlines[i + 1])
							writePosition += Width;
						else
						{
							for (var j = scanlines[i]; j < scanlines[i + 1]; j += 2)
							{
								writePosition += patterns[j];
								var pixels = patterns[j + 1];
								Array.Copy(data.ReadBytes(pixels), 0, Pixels, writePosition, pixels);
								writePosition += pixels;
							}

							if (writePosition % Width != 0)
								writePosition += Width - writePosition % Width;
						}
					}

					break;
				default:
					Log.Write("debug", "Unknown MixSprite type " + type);
					break;
			}
		}
	}
}
