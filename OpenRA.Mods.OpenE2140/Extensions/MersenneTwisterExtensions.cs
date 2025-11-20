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

using OpenRA.Support;

namespace OpenRA.Mods.OpenE2140.Extensions
{
	internal static class MersenneTwisterExtensions
	{
		public static int FromRange(this MersenneTwister mersenneTwister, int[] range)
		{
			if (range.Length == 1)
				return range[0];

			return mersenneTwister.Next(range[0], range[1]);
		}
	}
}
