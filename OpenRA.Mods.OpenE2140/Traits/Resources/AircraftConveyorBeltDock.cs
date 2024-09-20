using OpenRA.Activities;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Mods.OpenE2140.Traits.Resources.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

public class AircraftConveyorBeltDockInfo : SharedDockHostInfo
{
	public override object Create(ActorInitializer init)
	{
		return new AircraftConveyorBeltDock(init.Self, this);
	}
}

public class AircraftConveyorBeltDock : SharedDockHost
{
	public AircraftConveyorBeltDock(Actor self, AircraftConveyorBeltDockInfo info)
		: base(self, info)
	{
	}

	public override bool QueueMoveActivity(Activity moveToDockActivity, Actor self, Actor clientActor, DockClientManager client, MoveCooldownHelper moveCooldownHelper)
	{
		var aircraft = clientActor.Trait<Aircraft>();

		// Make sure the actor is at the dock and at correct facing.
		if (!clientActor.CenterPosition.EqualsHorizontally(this.DockPosition)
			|| aircraft.Facing != this.Info.DockAngle
			|| aircraft.GetPosition().Z != AircraftCrateLoad.LoadAltitude.Length)
		{
			moveCooldownHelper.NotifyMoveQueued();
			moveToDockActivity.QueueChild(aircraft.MoveOntoTarget(clientActor, Target.FromActor(self), this.DockPosition - self.CenterPosition, this.Info.DockAngle));
			return true;
		}

		return false;
	}
}
