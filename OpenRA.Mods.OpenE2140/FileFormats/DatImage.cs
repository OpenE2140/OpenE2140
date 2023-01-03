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

namespace OpenRA.Mods.E2140.FileFormats;

public class DatImage
{
	public readonly int Width;
	public readonly int Height;
	public readonly byte[] Pixels;

	public DatImage(Stream stream)
	{
		this.Width = stream.ReadUInt16();
		this.Height = stream.ReadUInt16();
		var unk = stream.ReadUInt16(); // TODO whas is this?!
		this.Pixels = stream.ReadBytes(this.Width * this.Height);
	}
}
