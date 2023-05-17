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

namespace OpenRA.Mods.OpenE2140.Extensions;

public static class ColorExtensions
{
	public static Color Lerp(this Color from, Color to, float progress)
	{
		return Color.FromArgb((byte)float2.Lerp(from.R, to.R, progress), (byte)float2.Lerp(from.G, to.G, progress), (byte)float2.Lerp(from.B, to.B, progress));
	}
}
