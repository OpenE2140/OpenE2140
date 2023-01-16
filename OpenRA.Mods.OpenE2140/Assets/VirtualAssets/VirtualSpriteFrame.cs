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

namespace OpenRA.Mods.OpenE2140.Assets.VirtualAssets;

public class VirtualSpriteFrame
{
	public readonly uint Width;
	public readonly uint Height;
	public readonly float2 Offset;
	public readonly byte[] Pixels;

	public VirtualSpriteFrame(uint width, uint height, float2 offset, byte[] pixels)
	{
		this.Width = width;
		this.Height = height;
		this.Offset = offset;
		this.Pixels = pixels;
	}
}
