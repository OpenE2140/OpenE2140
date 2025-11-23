using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

public class CrateTransporterAircraftInfo : AircraftInfo
{
	public override object Create(ActorInitializer init)
	{
		return new CrateTransporterAircraft(init, this);
	}
}

public class CrateTransporterAircraft : Aircraft
{
	private readonly Actor self;

	private DockClientManager? dockClientManager;

	public CrateTransporterAircraft(ActorInitializer init, AircraftInfo info)
		: base(init, info)
	{
		this.self = init.Self;
	}

	protected override void Created(Actor self)
	{
		base.Created(self);

		this.dockClientManager = self.Trait<DockClientManager>();
	}

	public override WVec GetRepulsionForce()
	{
		// The logic in Aircraft.GetRepulsionForce(), that temporarily disables the repulsion,
		// currently works only with Reservable trait and not the generic dock infrastructure.
		var dockHostActor = this.dockClientManager?.ReservedHostActor;
		if (dockHostActor != null)
		{
			var distanceFromDock = (dockHostActor.CenterPosition - this.self.CenterPosition).HorizontalLength;
			if (distanceFromDock < this.Info.WaitDistanceFromResupplyBase.Length)
				return WVec.Zero;
		}

		return base.GetRepulsionForce();
	}
}
