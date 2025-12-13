using OpenRA.Activities;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.OpenE2140.Extensions;

namespace OpenRA.Mods.OpenE2140.Traits.Resources.Activities;

public class AircraftDockWrapper : Activity
{
	private readonly Aircraft aircraft;
	private readonly AircraftCrateTransporter aircraftCrateTransporter;
	private readonly WithSpriteBody wsb;

	public AircraftDockWrapper(Actor self)
	{
		this.aircraft = self.Trait<Aircraft>();
		this.aircraftCrateTransporter = self.Trait<AircraftCrateTransporter>();
		this.wsb = self.Trait<WithSpriteBody>();
	}

	public override bool Tick(Actor self)
	{
		// Fix for bug #661.
		// There's a small chance that the docking process is interrupted just before the MoveToDock activity transitions
		// to/from the move activity to/from dock activity. At this moment, the aircraft still has influence on the ground.
		// Unless something removes this influence, the next time the aircraft attempts to land, an exception is thrown in Aircraft.AddInfluence(),
		// because the aircraft already has influence.
		// Therefore we need to make sure the aircraft properly takes off the conveyor belt, removing its influnce over the belt's cell.
		// In happy path scenario, IConveyorBeltDockHost.GetInnerDockActivity() in AircraftConveyorBeltDock is the place,
		// where the TakeOff activity is queued. And since the (un)docking skipped entirely, there's nothing else queuing the TakeOff activity.

		var dat = self.World.Map.DistanceAboveTerrain(this.aircraft.CenterPosition);

		if (this.IsCanceling && this.aircraft.HasInfluence())
		{
			if (this.wsb.DefaultAnimation.IsPlayingSequence(this.aircraftCrateTransporter.Info.DockSequence))
				this.wsb.CancelCustomAnimation(self);

			if (dat > this.aircraft.LandAltitude && dat < this.aircraft.Info.CruiseAltitude)
			{
				this.QueueChild(new TakeOff(self));
				return false;
			}

			this.aircraft.RemoveInfluence();
		}

		return true;
	}
}
