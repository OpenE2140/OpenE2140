using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Miner;

public class WallInfo : TraitInfo
{
	[GrantedConditionReference]
	[FieldLoader.Require]
	public readonly string? Condition;

	public override object Create(ActorInitializer init)
	{
		return new Wall(this);
	}
}

public class Wall
{
	private readonly WallInfo info;
	private int wallBuiltCondition;

	public Wall(WallInfo info)
	{
		this.info = info;
	}

	public void OnWallBuilt(Actor self)
	{
		if (!string.IsNullOrEmpty(this.info.Condition) && this.wallBuiltCondition == Actor.InvalidConditionToken)
			this.wallBuiltCondition = self.GrantCondition(this.info.Condition);
	}
}
