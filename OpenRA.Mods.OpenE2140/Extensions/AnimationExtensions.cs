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

namespace OpenRA.Mods.OpenE2140.Extensions
{
	public static class AnimationExtensions
	{
		public static bool IsPlayingSequence(this Animation animation, string sequence)
		{
			return sequence.Equals(animation.CurrentSequence?.Name, StringComparison.Ordinal);
		}
	}
}

