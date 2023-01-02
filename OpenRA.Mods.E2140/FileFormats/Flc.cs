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

public class Flc
{
	public readonly ushort Width;
	public readonly ushort Height;
	public readonly FlcFrame[] Frames;
	public readonly uint Speed;

	public Flc(Stream stream)
	{
		var size = stream.ReadUInt32();

		if (stream.ReadUInt16() != 44818)
			throw new Exception("Broken flc file!");

		this.Frames = new FlcFrame[stream.ReadUInt16()];
		this.Width = stream.ReadUInt16();
		this.Height = stream.ReadUInt16();

		var depth = stream.ReadUInt16();

		if (depth != 8)
			throw new Exception("Broken flc file!");

		var flags = stream.ReadUInt16();

		if (flags != 3)
			throw new Exception("Broken flc file!");

		this.Speed = stream.ReadUInt32();

		if (stream.ReadBytes(2).Any(b => b != 0x00))
			throw new Exception("Broken flc file!");

		var created = stream.ReadUInt32(); // TODO
		var creator = stream.ReadUInt32(); // TODO
		var updated = stream.ReadUInt32(); // TODO
		var updater = stream.ReadUInt32(); // TODO
		var aspectX = stream.ReadUInt16();
		var aspectY = stream.ReadUInt16();

		if (aspectX > 1 || aspectY > 1)
			throw new Exception("Broken flc file!");

		if (stream.ReadBytes(38).Any(b => b != 0x00))
			throw new Exception("Broken flc file!");

		var frame1 = stream.ReadUInt32(); // TODO
		var frame2 = stream.ReadUInt32(); // TODO

		if (stream.ReadBytes(40).Any(b => b != 0x00))
			throw new Exception("Broken flc file!");

		var palette = new Color[256];

		for (var i = 0; i <= this.Frames.Length; i++)
		{
			var chunkStart = stream.Position;
			var chunkSize = stream.ReadUInt32();

			if (stream.ReadUInt16() != 0xf1fa)
				throw new Exception("Broken flc file!");

			var frame = new FlcFrame(stream, palette, this, i == 0 ? new Color[this.Width * this.Height] : this.Frames[i - 1].Pixels);

			if (i < this.Frames.Length)
				this.Frames[i] = frame;

			if (stream.Position - chunkStart != chunkSize)
				throw new Exception("Broken flc file!");
		}

		if (stream.Position != size)
			throw new Exception("Broken flc file!");
	}
}
