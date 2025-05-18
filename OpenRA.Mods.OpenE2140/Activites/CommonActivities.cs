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

using OpenRA.Activities;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.OpenE2140.Activites;

public static class CommonActivities
{
	public static Activity? DragToPosition(Actor self, Mobile mobile, WPos targetPosition, CPos cell, int? speedModifier)
	{
		speedModifier ??= 100;

		var cellSpeed = mobile.MovementSpeedForCell(cell);
		var dragSpeed = cellSpeed * speedModifier.Value / 100;
		var ticksToDock = (self.CenterPosition - targetPosition).Length / dragSpeed;

		if (ticksToDock <= 0)
			return null;

		return new Drag(self, self.CenterPosition, targetPosition, ticksToDock);
	}
}
