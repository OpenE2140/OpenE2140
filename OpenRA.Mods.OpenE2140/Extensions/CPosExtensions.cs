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

namespace OpenRA.Mods.OpenE2140.Extensions;

public static class CPosExtensions
{
	/// <summary>
	/// Calculates bounding box from list of cells.
	/// </summary>
	/// <returns>Tuple of top left and bottom right world positions.</returns>
	public static (WPos TopLeft, WPos BottomRight) GetBounds(this IEnumerable<CPos> cells)
	{
		var left = int.MaxValue;
		var right = int.MinValue;
		var top = int.MaxValue;
		var bottom = int.MinValue;

		foreach (var cell in cells)
		{
			left = Math.Min(left, cell.X);
			right = Math.Max(right, cell.X);
			top = Math.Min(top, cell.Y);
			bottom = Math.Max(bottom, cell.Y);
		}

		return (
			TopLeft: new WPos(1024 * left, 1024 * top, 0),
			BottomRight: new WPos(1024 * right + 1024, 1024 * bottom + 1024, 0)
		);
	}
}
