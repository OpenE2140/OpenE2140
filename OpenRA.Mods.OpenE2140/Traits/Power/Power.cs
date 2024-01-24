using JetBrains.Annotations;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Power;

[Desc("Earth specific variant of the Power trait.")]
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class PowerInfo : ConditionalTraitInfo
{
	[Desc("If negative, it will drain power. If positive, it will provide power.")]
	public readonly int Amount;

	[GrantedConditionReference]
	[Desc("Grant this condition while the actor is powered.")]
	public readonly string Condition = "Powered";

	public override object Create(ActorInitializer init)
	{
		return new Power(init.Self, this);
	}
}

public class Power : ConditionalTrait<PowerInfo>, INotifyAddedToWorld, INotifyRemovedFromWorld, INotifyOwnerChanged
{
	private PowerManager playerPower;

	private int token = Actor.InvalidConditionToken;

	/// <summary>
	/// Returns <c>true</c>, if the actor currently has power.
	///
	/// This depends on whether it was intentionally disabled or there's currently not enough generated power.
	/// </summary>
	public bool IsPowered => this.token != Actor.InvalidConditionToken;

	public Power(Actor self, PowerInfo info)
		: base(info)
	{
		this.playerPower = self.Owner.PlayerActor.Trait<PowerManager>();
	}

	void INotifyAddedToWorld.AddedToWorld(Actor self)
	{
		this.playerPower.Add(self, this);
	}

	void INotifyRemovedFromWorld.RemovedFromWorld(Actor self)
	{
		this.playerPower.Remove(self);
	}

	void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
	{
		// Actor class always removes itself from world when changing owner (and re-adds itself once the transfer is complete).
		// Methods AddedToWorld and RemovedFromWorld above correctly handle this.
		// Adding this instance of Power trait to new player's PowerManager here (OnOwnerChanged) would cause AddedToWorld adding it second time.
		// There's no other place INotifyOwnerChanged.OnOwnerChanged callback is invoked, so there's no need to handle scenario, where ownership change
		// happens without actor being first removed from world (and added back after ownership has changed).
		this.playerPower = newOwner.PlayerActor.Trait<PowerManager>();
	}

	public void SetPowered(Actor self, bool powered)
	{
		if (powered && this.token == Actor.InvalidConditionToken)
			this.token = self.GrantCondition(this.Info.Condition);
		else if (!powered && this.token != Actor.InvalidConditionToken)
			this.token = self.RevokeCondition(this.token);
	}
}
