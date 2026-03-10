using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.World;

[Desc($"Grants a condition while the rush protection is active. Requires {nameof(RushProtection)} trait on World.")]
public class GrantConditionOnRushProtectionInfo : ConditionalTraitInfo, IRulesetLoaded
{
	[FieldLoader.Require]
	[GrantedConditionReference]
	[Desc("Condition to grant.")]
	public readonly string? Condition;

	void IRulesetLoaded<ActorInfo>.RulesetLoaded(Ruleset rules, ActorInfo info)
	{
		if (!rules.Actors[SystemActors.World].HasTraitInfo<RushProtectionInfo>())
			throw new YamlException($"{nameof(GrantConditionOnRushProtection)} requires {nameof(RushProtection)} defined on World.");
	}

	public override object Create(ActorInitializer init)
	{
		return new GrantConditionOnRushProtection(init.Self, this);
	}
}

public class GrantConditionOnRushProtection : ConditionalTrait<GrantConditionOnRushProtectionInfo>, INotifyRushProtection
{
	private readonly Actor self;

	private int conditionToken = Actor.InvalidConditionToken;


	public GrantConditionOnRushProtection(Actor self, GrantConditionOnRushProtectionInfo info)
		: base(info)
	{
		this.self = self;
	}

	void INotifyRushProtection.OnRushProtectionDisabled()
	{
		this.self.TryRevokingCondition(ref this.conditionToken);
	}

	void INotifyRushProtection.OnRushProtectionEnabled()
	{
		this.self.TryGrantingCondition(ref this.conditionToken, this.Info.Condition);
	}
}
