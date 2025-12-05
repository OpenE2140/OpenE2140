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

namespace OpenRA.Mods.OpenE2140.Assets.FileFormats
{
	public class DatImage
	{
		public readonly int Width;
		public readonly int Height;
		public readonly byte[] Pixels;

		public DatImage(Stream stream)
		{
			this.Width = stream.ReadUInt16();
			this.Height = stream.ReadUInt16();
			stream.ReadUInt8(); // TODO always 1
			stream.ReadUInt8(); // TODO id?
			this.Pixels = stream.ReadBytes(this.Width * this.Height);
		}
	}
}

