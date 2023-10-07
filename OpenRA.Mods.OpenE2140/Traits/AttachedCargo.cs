using System.Reflection;
using JetBrains.Annotations;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class AttachedCargoInfo : CargoInfo
{
	[Desc("The Z-Offset to ensure the passenger is drawn on top of the current actor.")]
	public int ZOffset = 1;

	public override object Create(ActorInitializer init)
	{
		return new AttachedCargo(init, this);
	}
}

public class AttachedCargo : Cargo, IRender, ITick, INotifyPassengerEntered
{
	private readonly AttachedCargoInfo info;

	public AttachedCargo(ActorInitializer init, AttachedCargoInfo info)
		: base(init, info)
	{
		this.info = info;
	}

	void ITick.Tick(Actor self)
	{
		var mobileType = typeof(Mobile);
		var actorType = typeof(Actor);

		foreach (var passenger in this.Passengers)
		{
			foreach (var mobile in passenger.TraitsImplementing<Mobile>())
			{
				// Use reflection to get around the movement and rotation animation code!

				mobile.Facing = self.Orientation.Yaw;

				mobileType.GetProperty("CenterPosition", BindingFlags.Instance | BindingFlags.Public)?.SetValue(mobile, self.CenterPosition);
				mobileType.GetField("oldFacing", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(mobile, mobile.Facing);
				mobileType.GetField("oldPos", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(mobile, mobile.CenterPosition);
			}

			// Use reflection as we need to fake the actor to be in the world. Otherwise it wont attack!
			actorType.GetProperty("IsInWorld", BindingFlags.Instance | BindingFlags.Public)?.SetValue(passenger, true);

			// We need to whitelist traits which we want to tick. Some are incompatible with this approach, like shroud revealing.
			foreach (var tick in passenger.TraitsImplementing<ITick>())
			{
				// TODO we might want this to be yaml settable, so we can simply extend it with other traits.

				// This trait is required for units to actualy shoot. Otherwise they will aim but never attack.
				if (tick is AttackTurreted)
					tick.Tick(passenger);
			}

			actorType.GetProperty("IsInWorld", BindingFlags.Instance | BindingFlags.Public)?.SetValue(passenger, false);
		}
	}

	void INotifyPassengerEntered.OnPassengerEntered(Actor self, Actor passenger)
	{
		foreach (var notifyAddedToWorld in passenger.TraitsImplementing<INotifyAddedToWorld>())
		{
			// TODO we might want this to be yaml settable, so we can simply extend it with other traits.

			// This trait is required for shadow unit. Otherwise the cloak is broken upon entering.
			if (notifyAddedToWorld is ProximityExternalCondition)
				notifyAddedToWorld.AddedToWorld(passenger);
		}
	}

	IEnumerable<IRenderable> IRender.Render(Actor self, WorldRenderer wr)
	{
		var result = new List<IRenderable>();

		foreach (var passenger in this.Passengers)
		foreach (var render in passenger.TraitsImplementing<IRender>())
		foreach (var renderable in render.Render(passenger, wr))
			result.Add(renderable.WithZOffset(this.info.ZOffset));

		return result;
	}

	IEnumerable<Rectangle> IRender.ScreenBounds(Actor self, WorldRenderer wr)
	{
		var result = new List<Rectangle>();

		foreach (var passenger in this.Passengers)
		foreach (var render in passenger.TraitsImplementing<IRender>())
			result.AddRange(render.ScreenBounds(passenger, wr));

		return result;
	}
}
