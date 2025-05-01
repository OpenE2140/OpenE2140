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

namespace OpenRA.Mods.OpenE2140.Traits.Research;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("Add this to a player, to add a research.")]
public class ResearchableInfo : TraitInfo, ITechTreePrerequisiteInfo
{
	[Desc("The internal id of the research.")]
	public readonly string Id = string.Empty;

	[Desc("The display name for this research.")]
	public readonly string Name = string.Empty;

	[Desc("The description for this research.")]
	public readonly string Description = string.Empty;

	[Desc("The level for this research.")]
	public readonly int Level;

	[Desc("The total duration for this research.")]
	public readonly int Duration = 1;

	[Desc("The total cost for this research.")]
	public readonly int Cost;

	[Desc("If set, only these factions can research this.")]
	public readonly string[] Factions = [];

	IEnumerable<string> ITechTreePrerequisiteInfo.Prerequisites(ActorInfo info)
	{
		yield return this.Id;
	}

	public override object Create(ActorInitializer init)
	{
		return new Researchable(this);
	}
}

public class Researchable
{
	public readonly ResearchableInfo Info;

	public int RemainingDuration;
	public int RemainingCost;
	public int PenaltySafeDuration;
	public int PenaltySafeCost;

	public Researchable(ResearchableInfo info)
	{
		this.Info = info;

		this.RemainingDuration = info.Duration;
		this.RemainingCost = info.Cost;
		this.PenaltySafeDuration = info.Duration;
		this.PenaltySafeCost = info.Cost;
	}
}
