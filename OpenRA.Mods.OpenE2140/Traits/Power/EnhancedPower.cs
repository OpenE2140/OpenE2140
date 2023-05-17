using JetBrains.Annotations;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Power;

[Desc("Earth specific variant of the Power trait.")]
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class EnhancedPowerInfo : ConditionalTraitInfo
{
	[Desc("If negative, it will drain power. If positive, it will provide power.")]
	public readonly int Amount;

	[GrantedConditionReference]
	[Desc("Grant this condition while the actor is powered.")]
	public readonly string Condition = "Powered";

	public override object Create(ActorInitializer init)
	{
		return new EnhancedPower(init.Self, this);
	}
}

public class EnhancedPower : ConditionalTrait<EnhancedPowerInfo>, INotifyAddedToWorld, INotifyRemovedFromWorld, INotifyOwnerChanged
{
	private EnhancedPowerManager playerPower;

	private int token = Actor.InvalidConditionToken;

	public EnhancedPower(Actor self, EnhancedPowerInfo info)
		: base(info)
	{
		this.playerPower = self.Owner.PlayerActor.Trait<EnhancedPowerManager>();
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
		this.playerPower.Remove(self);
		this.playerPower = newOwner.PlayerActor.Trait<EnhancedPowerManager>();
		this.playerPower.Add(self, this);
	}

	public void SetPowered(Actor self, bool powered)
	{
		if (powered && this.token == Actor.InvalidConditionToken)
			this.token = self.GrantCondition(this.Info.Condition);
		else if (!powered && this.token != Actor.InvalidConditionToken)
		{
			self.RevokeCondition(this.token);
			this.token = Actor.InvalidConditionToken;
		}
	}
}
