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
			moveToDockActivity.QueueChild(aircraft.MoveToTarget(clientActor, Target.FromPos(this.DockPosition), null, null));

			// Acquire lock early and keep it until the docking is complete
			moveToDockActivity.QueueChild(new DockHostLock(this,
				new AircraftCrateLoad.LandOnCrate(aircraft, Target.FromActor(self), () => this.Info.DockAngle, new(128)), releaseOnFinish: false));
			return true;
		}

		return false;
	}

	public override void QueueDockActivity(Activity moveToDockActivity, Actor self, Actor clientActor, DockClientManager client)
	{
		var dockActivity = new GenericDockSequence(
			clientActor,
			client,
			self,
			this,
			this.Info.DockWait,
			this.Info.IsDragRequired,
			this.Info.DragOffset,
			this.Info.DragLength);

		dockActivity.Queue(new TakeOff(clientActor));

		moveToDockActivity.QueueChild(new ReleaseDockHostLock(this, dockActivity));
	}
}
