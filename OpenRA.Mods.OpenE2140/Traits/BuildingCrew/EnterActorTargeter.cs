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

using OpenRA.Mods.Common.Orders;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.BuildingCrew;

public class EnterActorTargeter<T> : UnitOrderTargeter where T : ITraitInfoInterface
{
	private readonly string enterCursor;
	private readonly string enterBlockedCursor;
	private readonly Func<Actor, TargetModifiers, bool> canTarget;
	private readonly Func<FrozenActor, TargetModifiers, bool> canTargetFrozen;
	private readonly Func<Actor, bool> useEnterCursor;

	public EnterActorTargeter(string order, int priority, string enterCursor, string enterBlockedCursor,
		Func<Actor, TargetModifiers, bool> canTarget, Func<FrozenActor, TargetModifiers, bool> canTargetFrozen, Func<Actor, bool> useEnterCursor)
		: base(order, priority, enterCursor, true, true)
	{
		this.enterCursor = enterCursor;
		this.enterBlockedCursor = enterBlockedCursor;
		this.canTarget = canTarget;
		this.canTargetFrozen = canTargetFrozen;
		this.useEnterCursor = useEnterCursor;
	}

	public override bool CanTargetActor(Actor self, Actor target, TargetModifiers modifiers, ref string cursor)
	{
		if (!target.Info.HasTraitInfo<T>() || !this.canTarget(target, modifiers))
			return false;

		cursor = this.useEnterCursor(target) ? this.enterCursor : this.enterBlockedCursor;
		return true;
	}

	public override bool CanTargetFrozenActor(Actor self, FrozenActor target, TargetModifiers modifiers, ref string cursor)
	{
		if (!target.Info.HasTraitInfo<T>() || !this.canTargetFrozen(target, modifiers))
			return false;

		cursor = this.useEnterCursor(target.Actor) ? this.enterCursor : this.enterBlockedCursor;
		return true;
	}
}
