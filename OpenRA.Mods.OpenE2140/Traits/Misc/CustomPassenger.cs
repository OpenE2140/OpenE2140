using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Misc;

[Desc($"This actor can enter Cargo actors. Custom version of {nameof(Passenger)} trait, which can optionally preserve passenger's activities on unload.")]
public class CustomPassengerInfo : PassengerInfo
{
    [Desc("When unloading, cancel all other activies.")]
    public readonly bool CancelActivitiesOnExit = true;

    public override object Create(ActorInitializer init) { return new CustomPassenger(this); }
}

public class CustomPassenger : Passenger
{
	public new CustomPassengerInfo Info { get; }

	public CustomPassenger(CustomPassengerInfo info)
		: base(info)
	{
		this.Info = info;
	}

	public override void OnBeforeAddedToWorld(Actor actor)
	{
		if (this.Info.CancelActivitiesOnExit)
			actor.CancelActivity();
	}

	public override void OnEjectedFromKilledCargo(Actor self)
	{
		// It's possible that the passenger had some activities queued up before entering the Cargo.
		// If so, we need to queue the Nudge activity as child of that activity
		
		self.CurrentActivity.QueueChild(new Nudge(self));
	}
}
