using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Miner;

public class GrantConditionOnWallBuildingInfo : ConditionalTraitInfo, Requires<WallBuilderInfo>
{
	[FieldLoader.Require]
	[GrantedConditionReference]
	[Desc("Condition to grant.")]
	public readonly string? Condition = null;

	public override object Create(ActorInitializer init)
	{
		return new GrantConditionOnWallBuilding(this);
	}
}

public class GrantConditionOnWallBuilding : ConditionalTrait<GrantConditionOnWallBuildingInfo>, INotifyWallBuilding
{
	private int conditionToken = Actor.InvalidConditionToken;

	public GrantConditionOnWallBuilding(GrantConditionOnWallBuildingInfo info)
		: base(info)
	{
	}

	void INotifyWallBuilding.WallBuilding(Actor self, CPos location)
	{
		self.GrantOrRevokeCondition(ref this.conditionToken, true, this.Info.Condition);
	}

	void INotifyWallBuilding.WallBuildingCanceled(Actor self, CPos location)
	{
		self.GrantOrRevokeCondition(ref this.conditionToken, false, this.Info.Condition);
	}

	void INotifyWallBuilding.WallBuildingCompleted(Actor self, CPos location)
	{
		self.GrantOrRevokeCondition(ref this.conditionToken, false, this.Info.Condition);
	}

	void INotifyWallBuilding.WallCreated(Actor self, Actor wall)
	{
		// noop
	}
}
