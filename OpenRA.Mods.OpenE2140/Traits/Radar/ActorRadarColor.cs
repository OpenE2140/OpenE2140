#region Copyright & License Information

/*
 * Copyright 2007-2023 The OpenE2140 Developers (see AUTHORS)
 * This file is part of OpenE2140, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */

#endregion

using JetBrains.Annotations;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Radar;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("Changes what color actor has on minimap. Applied to color as HSV.")]
public class ActorRadarColorInfo : TraitInfo
{
	[Desc("Delta applied to H")]
	public readonly float DeltaH;

	[Desc("Delta applied to S")]
	public readonly float DeltaS;

	[Desc("Delta applied to V")]
	public readonly float DeltaV;

	public override object Create(ActorInitializer init)
	{
		return new ActorRadarColor(this);
	}
}

public class ActorRadarColor : IRadarColorModifier
{
	private readonly ActorRadarColorInfo info;

	public ActorRadarColor(ActorRadarColorInfo info)
	{
		this.info = info;
	}

	public Color RadarColorOverride(Actor self, Color color)
	{
		var (a, h, s, v) = color.ToAhsv();

		return Color.FromAhsv(a, h + this.info.DeltaH, s + this.info.DeltaS, v + this.info.DeltaV);
	}
}
