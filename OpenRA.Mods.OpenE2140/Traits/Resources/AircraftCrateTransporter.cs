using OpenRA.Activities;
using OpenRA.Mods.OpenE2140.Traits.Resources.Activities;

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

public class AircraftCrateTransporterInfo : CrateTransporterInfo
{
	[Desc("List of angles, at which the aircraft crate transporter can land/dock.")]
	public readonly WAngle[] AllowedDockAngles = { new(0) };

	[Desc("Altitude at which the aircraft considers itself landed with a resource crate loaded.")]
	public readonly WDist LandAltitude = new(128);

	public override object Create(ActorInitializer init)
	{
		return new AircraftCrateTransporter(init, this);
	}
}

public class AircraftCrateTransporter : CrateTransporter
{
	public new AircraftCrateTransporterInfo Info;

	public AircraftCrateTransporter(ActorInitializer init, AircraftCrateTransporterInfo info)
		: base(init, info)
	{
		this.Info = info;
	}

	protected override Activity GetCrateLoadActivity(Actor self, Order order)
	{
		return new AircraftCrateLoad(self, order.Target, this.Info);
	}
}
