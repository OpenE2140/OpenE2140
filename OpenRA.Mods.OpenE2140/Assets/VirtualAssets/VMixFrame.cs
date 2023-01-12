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

namespace OpenRA.Mods.E2140.Assets.VirtualAssets;

public class VMixFrame
{
	public readonly uint Width;
	public readonly uint Height;
	public readonly byte[] Pixels;

	public VMixFrame(uint width, uint height, byte[] pixels)
	{
		this.Width = width;
		this.Height = height;
		this.Pixels = pixels;
	}
}
