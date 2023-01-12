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

using OpenRA.Graphics;

namespace OpenRA.Mods.E2140.Graphics;

public class CutOffAnimationWithOffset : AnimationWithOffset
{
	public readonly Func<int> Bottom;

	public CutOffAnimationWithOffset(Animation a, Func<WVec> offset, Func<bool> disable, Func<WPos, int> zOffset, Func<int> bottom)
		: base(a, offset, disable, zOffset)
	{
		this.Bottom = bottom;
	}
}
