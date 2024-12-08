using OpenRA.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Traits.Resources.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

public class AircraftCrateTransporterInfo : CrateTransporterInfo
{
	[Desc("List of angles, at which the aircraft crate transporter can land/dock.")]
	public readonly WAngle[] AllowedDockAngles = { new(0) };

	[Desc("Altitude at which the aircraft considers itself landed with a resource crate loaded.")]
	public readonly WDist LandAltitude = WDist.Zero;

	public override object Create(ActorInitializer init)
	{
		return new AircraftCrateTransporter(init, this);
	}
}

public class AircraftCrateTransporter : CrateTransporter, IAircraftCenterPositionOffset
{
	private readonly Actor self;
	private readonly BodyOrientation body;

	public new AircraftCrateTransporterInfo Info;

	public AircraftCrateTransporter(ActorInitializer init, AircraftCrateTransporterInfo info)
		: base(init, info)
	{
		this.Info = info;

		this.self = init.Self;
		this.body = init.Self.Trait<BodyOrientation>();
	}

	WVec IAircraftCenterPositionOffset.PositionOffset
	{
		get
		{
			if (!this.HasCrate) return WVec.Zero;

			var localOffset = new WVec(0, 0, -this.Info.LandAltitude.Length).Rotate(this.body.QuantizeOrientation(this.self.Orientation));
			return this.body.LocalToWorld(localOffset);
		}
	}

	protected override Activity GetCrateUnloadActivity(Actor self, Order order)
	{
		CPos? targetLocation = order.Target.Type != TargetType.Invalid ? self.World.Map.CellContaining(order.Target.CenterPosition) : null;
		return new AircraftCrateUnload(self, targetLocation, this.Info);
	}

	protected override Activity GetCrateLoadActivity(Actor self, Order order)
	{
		return new AircraftCrateLoad(self, order.Target, this.Info);
	}
}
