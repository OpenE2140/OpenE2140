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

using OpenRA.Primitives;

namespace OpenRA.Mods.OpenE2140.Assets.FileFormats
{
	public class MixFrame
	{
		public readonly ushort Width;
		public readonly ushort Height;
		public readonly byte Palette;
		public readonly byte[] Pixels;
		public readonly bool Is32Bpp;

		public MixFrame(Stream stream)
		{
			this.Width = stream.ReadUInt16();
			this.Height = stream.ReadUInt16();
			var type = stream.ReadUInt8();
			this.Palette = stream.ReadUInt8();

			switch (type)
			{
				case 1:
					this.Pixels = stream.ReadBytes(this.Width * this.Height);

					break;

				case 2:
					this.Pixels = new byte[this.Width * this.Height * 4];
					this.Is32Bpp = true;

					for (var i = 0; i < this.Pixels.Length; i += 4)
					{
						var color16 = stream.ReadUInt16();
						this.Pixels[i + 0] = (byte)((color16 & 0xf800) >> 8);
						this.Pixels[i + 1] = (byte)((color16 & 0x07e0) >> 3);
						this.Pixels[i + 2] = (byte)((color16 & 0x001f) << 3);
						this.Pixels[i + 3] = 0xff;
					}

					break;

				case 9:
					this.Pixels = new byte[this.Width * this.Height];

					var widthCopy = stream.ReadUInt32();
					var heightCopy = stream.ReadUInt32();

					if (this.Width != widthCopy || this.Height != heightCopy)
						throw new Exception("Broken mix frame!");

					var dataSize = stream.ReadUInt32();
					var numScanlines = stream.ReadUInt32();
					var numPatterns = stream.ReadUInt32();
					var scanLinesOffset = stream.ReadUInt32();
					var dataOffsetsOffset = stream.ReadUInt32();
					var patternsOffset = stream.ReadUInt32();
					var compressedImageDataOffset = stream.ReadUInt32();

					if (scanLinesOffset != stream.Position - 6)
						throw new Exception("Broken mix frame!");

					var scanlines = new int[numScanlines];

					for (var i = 0; i < numScanlines; i++)
						scanlines[i] = stream.ReadUInt16();

					if (dataOffsetsOffset != stream.Position - 6)
						throw new Exception("Broken mix frame!");

					var dataOffsets = new int[numScanlines];

					for (var i = 0; i < numScanlines; i++)
						dataOffsets[i] = stream.ReadUInt16();

					if (patternsOffset != stream.Position - 6)
						throw new Exception("Broken mix file!");

					var patterns = stream.ReadBytes((int)numPatterns);
					var data = new SegmentStream(stream, compressedImageDataOffset + 6, dataSize);

					var writePosition = 0;

					for (var i = 0; i < this.Height; i++)
					{
						data.Position = dataOffsets[i];

						if (scanlines[i] == scanlines[i + 1])
							writePosition += this.Width;
						else
						{
							for (var j = scanlines[i]; j < scanlines[i + 1]; j += 2)
							{
								writePosition += patterns[j];
								var pixels = patterns[j + 1];
								Array.Copy(data.ReadBytes(pixels), 0, this.Pixels, writePosition, pixels);
								writePosition += pixels;
							}

							if (writePosition % this.Width != 0)
								writePosition += this.Width - writePosition % this.Width;
						}
					}

					break;

				default:
					throw new Exception("Unknown MixSprite type " + type);
			}

			// Some sprites have sizes non-dividable by two. When OpenRA renders sprites, the origin is in the middle of the sprite
			// (not top-left corner like in E2140). Unfortunately with sprites, which have size non-dividable by two, this means that the sprite
			// will be distorted, when rendered. This is the origin of the sprite will not be exactly aligned at a pixel (since diving odd number results
			// in non-integer number) and since OpenRA rounds the numbers (when it comes to rendering sprites), the rendered sprites will usually shrink a bit.

			// This hack aligns both width and height of the sprite to a number dividable by two, hence the rounding does not happen anymore.
			// Unfortunately this means that the sprites are tiny bit larger than they need to be, plus many sprites will need adjusting their offsets
			// to make them look good again. Unfortunately this means that the sprites will take a bit more memory than they need, plus many sprites
			// will need their offsets adjusted. But this is a one-time thing, on the other hand the memory usage should be looked into,
			// when optimizing the code in the future.
			{
				if (this.Height % 2 == 0 && this.Width % 2 == 0)
					return;

				var width = this.Width + this.Width % 2;
				var height = this.Height + this.Height % 2;
				var pixels = new byte[width * height];

				for (var y = 0; y < this.Height; y++)
					Array.Copy(this.Pixels, y * this.Width, pixels, (y + this.Height % 2) * width + this.Width % 2, this.Width);

				this.Width = (ushort)width;
				this.Height = (ushort)height;
				this.Pixels = pixels;
			}
		}
	}
}

