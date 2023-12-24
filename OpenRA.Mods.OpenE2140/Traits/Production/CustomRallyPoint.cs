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

using OpenRA.Effects;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Traits.Effects;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Production;

[Desc("Used to waypoint units after production or repair is finished.")]
public class CustomRallyPointInfo : RallyPointInfo
{
	[Desc(
		"Override offset at which the first point of rally point path is drawn relative relative to the center of the producing actor.",
		$"If not specified, {nameof(ExitInfo.SpawnOffset)} from {nameof(Exit)} trait with nearest {nameof(ExitInfo.ExitCell)} is used."
	)]
	public readonly WVec? LineInitialOffset = null;

	public override object Create(ActorInitializer init)
	{
		return new CustomRallyPoint(init.Self, this);
	}
}

public class CustomRallyPoint : RallyPoint, INotifyCreated, INotifyAddedToWorld
{
	public new CustomRallyPointInfo Info => (CustomRallyPointInfo)base.Info;

	public CustomRallyPoint(Actor self, RallyPointInfo info)
		: base(self, info)
	{
	}

	protected override IEffect CreateRallyPointIndicator(Actor self)
	{
		return new CustomRallyPointIndicator(self, this);
	}
}
