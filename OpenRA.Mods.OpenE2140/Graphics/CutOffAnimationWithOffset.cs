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

using OpenRA.Graphics;

namespace OpenRA.Mods.OpenE2140.Graphics
{
	public enum CutOffDirection
	{
		Bottom,
		Top
	}

	public class CutOffAnimationWithOffset : AnimationWithOffset
	{
		public readonly CutOffDirection Direction;

		public readonly Func<int> CutOff;

		public CutOffAnimationWithOffset(Animation a, Func<WVec> offset, Func<bool> disable, Func<WPos, int> zOffset, Func<int> cutOff, CutOffDirection direction)
			: base(a, offset, disable, zOffset)
		{
			this.CutOff = cutOff;
			this.Direction = direction;
		}
	}
}

