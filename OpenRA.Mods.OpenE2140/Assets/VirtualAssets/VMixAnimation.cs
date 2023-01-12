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

public class VMixAnimation
{
	public readonly string Name;
	public readonly byte Facings;
	public readonly VMixFrame[] Frames;

	public VMixAnimation(string name, byte facings, VMixFrame[] frames)
	{
		this.Name = name;
		this.Facings = facings;
		this.Frames = frames;
	}
}
