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
