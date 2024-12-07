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
	[Desc(
		"List of angles, which the aircraft crate transporter can dock into this dock host.",
		$"All angles must be also present in {nameof(AircraftCrateTransporter)}.{nameof(AircraftCrateTransporterInfo.AllowedDockAngles)}.",
		$"In other words, angles in {nameof(AircraftConveyorBeltDock)} are subset of angles in {nameof(AircraftCrateTransporter)}")]
	public readonly WAngle[] AllowedDockAngles = { new(0) };

	[Desc($"Altitude at which the aircraft considers itself landed with on top of the {nameof(AircraftConveyorBeltDockInfo)}.")]
	public readonly WDist LandAltitude = new(128);

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
			|| aircraft.GetPosition().Z != this.Info.LandAltitude.Length)
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
		private readonly AircraftCrateTransporter crateTransporter;

		private WPos DockPosition => this.aircraftConveyorBeltDock.DockPosition;

		private readonly WAngle[] allowedDockAngles;

		public AircraftMoveToConveyorBelt(Actor self, AircraftConveyorBeltDock aircraftConveyorBeltDock)
		{
			this.aircraft = self.Trait<Aircraft>();
			this.aircraftConveyorBeltDock = aircraftConveyorBeltDock;
			this.crateTransporter = self.Trait<AircraftCrateTransporter>();

			// Allowed dock angles are restricted by the angles allowed by conveyor belt dock and the crate transporter itself
			this.allowedDockAngles = this.aircraftConveyorBeltDock.Info.AllowedDockAngles.Intersect(this.crateTransporter.Info.AllowedDockAngles).ToArray();
		}

		protected override void OnFirstRun(Actor self)
		{
			this.QueueChild(this.aircraft.MoveToTarget(self, Target.FromPos(this.DockPosition), null, null));

			var sequence = self.Trait<WithSpriteBody>().DefaultAnimation.GetSequence(this.crateTransporter.Info.DockSequence);

			// Acquire lock now (i.e. before the landing starts) and keep it until the docking is complete
			this.QueueChild(new DockHostLock(this.aircraftConveyorBeltDock,
				new AircraftCrateLoad.LandOnCrate(
					this.aircraft, Target.FromActor(self), GetDockAngle,
					this.aircraftConveyorBeltDock.Info.LandAltitude), releaseOnFinish: false));

			WAngle GetDockAngle() => this.allowedDockAngles
				.OrderBy(x => new WAngle((x.Angle - Util.QuantizeFacing(self.Orientation.Yaw, sequence.Facings).Angle) * Util.GetTurnDirection(self.Orientation.Yaw, x)).Angle)
				.FirstOrDefault();
		}
	}
}
