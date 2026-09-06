using System.Reflection;
using JetBrains.Annotations;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Support;
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

public class AttachedCargo : Cargo, IRender, ITick, INotifyPassengerEntered, INotifyPassengerExited
{
	private readonly AttachedCargoInfo info;
	private readonly BooleanExpression? externalConditionExpression;
	private readonly ProximityExternalCondition? proximityExternalCondition;
	private readonly Dictionary<Actor, int> passengerExternalConditions = [];
	private bool? hasExternalCondition;

	public AttachedCargo(ActorInitializer init, AttachedCargoInfo info)
		: base(init, info)
	{
		this.info = info;
		this.proximityExternalCondition = init.Self.TraitOrDefault<ProximityExternalCondition>();
		if (this.proximityExternalCondition != null)
		{
			this.externalConditionExpression = new BooleanExpression(this.proximityExternalCondition.Info.Condition);
		}
	}

	public override IEnumerable<VariableObserver> GetVariableObservers()
	{
		foreach (var observer in base.GetVariableObservers())
			yield return observer;

		if (this.proximityExternalCondition != null && !string.IsNullOrEmpty(this.proximityExternalCondition.Info.Condition))
		{
			yield return new VariableObserver(this.ExternalConditionGranted, [this.proximityExternalCondition.Info.Condition]);
		}
	}

	private void ExternalConditionGranted(Actor self, IReadOnlyDictionary<string, int> conditions)
	{
		this.hasExternalCondition = this.externalConditionExpression?.Evaluate(conditions);
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
				if (tick is AttackTurreted or AttackFrontal or Cloak)
					tick.Tick(passenger);
			}

			actorType.GetProperty("IsInWorld", BindingFlags.Instance | BindingFlags.Public)?.SetValue(passenger, false);
		}
	}

	void INotifyPassengerEntered.OnPassengerEntered(Actor self, Actor passenger)
	{
		var pecPassenger = passenger.TraitOrDefault<ProximityExternalCondition>()?.Info.Condition;
		var pecCargo = self.TraitOrDefault<ProximityExternalCondition>()?.Info.Condition;

		// This hack is necessary, because the Passenger actor isn't granted the condition the Cargo actor provides.
		if (!string.IsNullOrEmpty(pecPassenger) && pecPassenger == pecCargo)
		{
			var external = passenger.TraitsImplementing<ExternalCondition>()
				.FirstOrDefault(t => t.Info.Condition == pecPassenger && t.CanGrantCondition(self));
			if (external != null)
			{
				this.passengerExternalConditions[passenger] = external.GrantCondition(passenger, self);
			}
		}
	}

	void INotifyPassengerExited.OnPassengerExited(Actor self, Actor passenger)
	{
		var pecPassenger = passenger.TraitOrDefault<ProximityExternalCondition>()?.Info.Condition;
		var pecCargo = this.proximityExternalCondition?.Info.Condition;
		if (!string.IsNullOrEmpty(pecCargo) && (pecPassenger == pecCargo || this.hasExternalCondition == true))
		{
			// This hack is neccessary, because ProximityExternalCondition grants the condition in next tick (due to logic in ActorMap).
			// This means that when passenger exits transporter like WTP 100, it doesn't re-cloak itself for split second (i.e. one tick),
			// if it's still inside of cloaking radius.
			// The hack makes sure the cloaking condition is temporarily granted, until nearby Shadow can grant this condition itself.
			var external = passenger
				.TraitsImplementing<ExternalCondition>()
				.FirstOrDefault(t => t.Info.Condition == pecCargo && t.CanGrantCondition(self));
			if (external != null)
			{
				external?.GrantCondition(passenger, self, duration: 2);
			}
		}

		if (this.passengerExternalConditions.Remove(passenger, out var token))
			passenger.RevokeCondition(token);
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
