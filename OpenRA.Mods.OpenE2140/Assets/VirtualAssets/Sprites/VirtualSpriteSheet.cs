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

namespace OpenRA.Mods.OpenE2140.Assets.VirtualAssets.Sprites;

public class VirtualSpriteSheet
{
	public readonly VirtualSpriteAnimation[] Animations;

	public VirtualSpriteSheet(VirtualSpriteAnimation[] animations)
	{
		this.Animations = animations;
	}
}
