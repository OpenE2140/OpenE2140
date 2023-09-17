using JetBrains.Annotations;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Misc;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("This actor will be visible for a particular time when being killed.")]
public class FrozenOnDeathInfo : TraitInfo, Requires<HealthInfo>
{
	[Desc("The amount of ticks the death state will be visible.")]
	public readonly int Duration = 25;

	[GrantedConditionReference]
	[Desc("The condition to grant to self while the death is being delayed.")]
	public readonly string? Condition;

	public override object Create(ActorInitializer init)
	{
		return new FrozenOnDeath(init.Self, this);
	}
}

public class FrozenOnDeath : ITick
{
	private readonly FrozenOnDeathInfo info;
	private int despawn;
	private int diedToken = Actor.InvalidConditionToken;

	public FrozenOnDeath(Actor self, FrozenOnDeathInfo info)
	{
		this.info = info;
		this.despawn = info.Duration;
		self.Trait<Health>().RemoveOnDeath = false;
	}

	void ITick.Tick(Actor self)
	{
		if (!self.IsDead)
			return;

		if (this.diedToken == Actor.InvalidConditionToken)
			this.diedToken = self.GrantCondition(this.info.Condition);

		if (--this.despawn <= 0)
		{
			if (this.diedToken != Actor.InvalidConditionToken)
				this.diedToken = self.RevokeCondition(this.diedToken);

			self.Dispose();
		}
	}
}
