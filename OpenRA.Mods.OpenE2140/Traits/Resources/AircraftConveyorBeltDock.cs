using OpenRA.Activities;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Mods.OpenE2140.Traits.Resources.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

public class AircraftConveyorBeltDockInfo : SharedDockHostInfo
{
	[Desc("List of angles, which the aircraft crate transporter can dock into this dock host.")]
	public readonly WAngle[] AllowedDockAngles = { new(0) };

	public override object Create(ActorInitializer init)
	{
		return new AircraftConveyorBeltDock(init.Self, this);
	}
}

public class AircraftConveyorBeltDock : SharedDockHost, IConveyorBeltDockHost
{
	public new readonly AircraftConveyorBeltDockInfo Info;

	public AircraftConveyorBeltDock(Actor self, AircraftConveyorBeltDockInfo info)
		: base(self, info)
	{
		this.Info = info;
	}

	public override bool QueueMoveActivity(Activity moveToDockActivity, Actor self, Actor clientActor, DockClientManager client, MoveCooldownHelper moveCooldownHelper)
	{
		var aircraft = clientActor.Trait<Aircraft>();

		// Make sure the actor is at the dock and at correct facing.
		if (!clientActor.CenterPosition.EqualsHorizontally(this.DockPosition)
			|| this.Info.AllowedDockAngles.IndexOf(aircraft.Facing) == -1
			|| aircraft.GetPosition().Z != AircraftCrateLoad.LoadAltitude.Length)
		{
			moveCooldownHelper.NotifyMoveQueued();

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

	Activity IConveyorBeltDockHost.GetInnerDockActivity(Actor self, Actor clientActor, Action continuationCallback, ConveyorBeltInnerDockContext context)
	{
		// TODO: animation
		continuationCallback();
		return null!;
	}

	private class AircraftMoveToConveyorBelt : Activity
	{
		private readonly Aircraft aircraft;
		private readonly AircraftConveyorBeltDock aircraftConveyorBeltDock;
		private readonly CrateTransporter crateTransporter;

		private WPos DockPosition => this.aircraftConveyorBeltDock.DockPosition;

		private WAngle[] AllowedDockAngles => this.aircraftConveyorBeltDock.Info.AllowedDockAngles;

		public AircraftMoveToConveyorBelt(Actor self, AircraftConveyorBeltDock aircraftConveyorBeltDock)
		{
			this.aircraft = self.Trait<Aircraft>();
			this.aircraftConveyorBeltDock = aircraftConveyorBeltDock;
			this.crateTransporter = self.Trait<CrateTransporter>();
		}

		protected override void OnFirstRun(Actor self)
		{
			this.QueueChild(this.aircraft.MoveToTarget(self, Target.FromPos(this.DockPosition), null, null));

			var sequence = self.Trait<WithSpriteBody>().DefaultAnimation.GetSequence(this.crateTransporter.Info.DockSequence);

			// TODO: make landing altitude configurable
			WDist landingAltitude = new(128);

			// Acquire lock now (i.e. before the landing starts) and keep it until the docking is complete
			this.QueueChild(new DockHostLock(this.aircraftConveyorBeltDock,
				new AircraftCrateLoad.LandOnCrate(this.aircraft, Target.FromActor(self), GetDockAngle, landingAltitude), releaseOnFinish: false));

			WAngle GetDockAngle() => this.AllowedDockAngles
				.OrderBy(x => new WAngle((x.Angle - Util.QuantizeFacing(self.Orientation.Yaw, sequence.Facings).Angle) * Util.GetTurnDirection(self.Orientation.Yaw, x)).Angle)
				.FirstOrDefault();
		}
	}
}
