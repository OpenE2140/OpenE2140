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
	public class MixPalette
	{
		public readonly Color[] Colors = new Color[256];

		public MixPalette(Stream stream)
		{
			for (var i = 0; i < this.Colors.Length; i++)
			{
				var r = stream.ReadUInt8();
				var g = stream.ReadUInt8();
				var b = stream.ReadUInt8();

				// Pixels in the color range of the color remapping.
				if (r == b && r != 0)
					r--;

				this.Colors[i] = Color.FromArgb(i == 0 ? 0x00 : 0xff, r, g, b);
			}
		}
	}
}

