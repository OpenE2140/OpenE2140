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
