using OpenRA.Activities;
using OpenRA.Mods.OpenE2140.Traits.Resources.Activities;

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

public class AircraftCrateTransporterInfo : CrateTransporterInfo
{
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
		return new AircraftCrateLoad(self, order.Target);
	}
}
