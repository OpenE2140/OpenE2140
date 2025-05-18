#region Copyright & License Information

/*
 * Copyright (c) The OpenE2140 Developers and Contributors
 * This file is part of OpenE2140, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */

#endregion

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
