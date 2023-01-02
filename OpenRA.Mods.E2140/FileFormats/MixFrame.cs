#region Copyright & License Information

/*
 * Copyright 2007-2022 The Earth 2140 Developers (see AUTHORS)
 * This file is part of Earth 2140, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */

#endregion

using OpenRA.Primitives;

namespace OpenRA.Mods.E2140.FileFormats;

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

				var widthCopy = stream.ReadInt32();
				var heightCopy = stream.ReadInt32();

				if (this.Width != widthCopy || this.Height != heightCopy)
					throw new Exception("Broken mix frame!");

				var dataSize = stream.ReadInt32();
				var numScanlines = stream.ReadInt32();
				var numPatterns = stream.ReadInt32();
				var scanLinesOffset = stream.ReadInt32();
				var dataOffsetsOffset = stream.ReadInt32();
				var patternsOffset = stream.ReadInt32();
				var compressedImageDataOffset = stream.ReadInt32();

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

				var patterns = stream.ReadBytes(numPatterns);
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
	}
}
