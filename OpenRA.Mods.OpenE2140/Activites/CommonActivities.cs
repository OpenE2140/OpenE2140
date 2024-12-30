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
