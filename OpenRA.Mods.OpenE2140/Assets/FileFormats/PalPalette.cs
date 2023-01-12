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

using OpenRA.Primitives;

namespace OpenRA.Mods.E2140.Assets.FileFormats;

public class PalPalette
{
	public readonly Color[] Colors = new Color[256];

	public PalPalette(Stream stream)
	{
		for (var i = 0; i < this.Colors.Length; i++)
			this.Colors[i] = Color.FromArgb(0xff, stream.ReadUInt8(), stream.ReadUInt8(), stream.ReadUInt8());
	}
}
