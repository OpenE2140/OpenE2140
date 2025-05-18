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

namespace OpenRA.Mods.OpenE2140.Traits.Resources.Activities;

public static class CrateLoadUnloadHelpers
{
	public static readonly int NonDiagonalDockDistance = 405;
	public static readonly int DiagonalDockDistance = 570;

	public static WVec GetDockVector(CVec vector)
	{
		var isDiagonal = vector.X != 0 && vector.Y != 0;

		return new WVec(vector.X, vector.Y, 0) * (isDiagonal ? DiagonalDockDistance : NonDiagonalDockDistance);
	}
}
