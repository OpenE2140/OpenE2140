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

using OpenRA.Primitives;

namespace OpenRA.Mods.E2140.FileFormats;

public class Pcx
{
	public readonly ushort X;
	public readonly ushort Y;
	public readonly ushort Width;
	public readonly ushort Height;
	public readonly Color[] Pixels;

	public Pcx(Stream stream)
	{
		if (stream.ReadByte() != 0x0a)
			throw new Exception("Broken pcx file!");

		var version = stream.ReadByte();
		var encoding = stream.ReadByte();
		var bpp = stream.ReadByte();

		if (version != 5 || encoding != 1 || bpp != 8)
			throw new Exception("Broken pcx file!");

		this.X = stream.ReadUInt16();
		this.Y = stream.ReadUInt16();
		this.Width = stream.ReadUInt16();
		this.Height = stream.ReadUInt16();
		this.Pixels = new Color[this.Width * this.Height];

		stream.Position += 52; // dpi, ega palette

		var reserved1 = stream.ReadByte();
		var channels = stream.ReadByte();
		var lineWidth = stream.ReadUInt16();
		var paletteType = stream.ReadUInt16();

		if (reserved1 != 0 || channels != 1 || paletteType != 1)
			throw new Exception("Broken pcx file!");

		stream.Position += 4; // resolution

		if (stream.ReadBytes(54).Any(b => b != 0x00))
			throw new Exception("Broken pcx file!");

		stream.Position = stream.Length - 768;
		var palette = new Color[256];

		for (var i = 0; i < palette.Length; i++)
			palette[i] = Color.FromArgb(0xff, stream.ReadUInt8(), stream.ReadUInt8(), stream.ReadUInt8());

		stream.Position = 128;

		try
		{
			for (var y = 0; y < this.Height; y++)
			{
				for (var x = 0; x < lineWidth;)
				{
					var count = 1;
					var value = stream.ReadByte();

					if (value >> 6 == 0x3)
					{
						count = value & 0x3f;
						value = stream.ReadByte();
					}

					for (var i = 0; i < count; i++)
					{
						if (x < this.Width)
							this.Pixels[y * this.Width + x] = palette[value];

						x++;
					}
				}
			}
		}
		catch (Exception e)
		{
			Console.WriteLine(e);
		}
	}
}
