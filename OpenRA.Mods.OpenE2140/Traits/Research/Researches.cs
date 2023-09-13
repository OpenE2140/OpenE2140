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
[Desc("Add this to an actor, which is able to research.")]
public class ResearchesInfo : PausableConditionalTraitInfo
{
	public override object Create(ActorInitializer init)
	{
		return new Researches(this);
	}
}

public class Researches : PausableConditionalTrait<ResearchesInfo>, ITick, INotifyAddedToWorld, INotifyOwnerChanged
{
	private Research? research;

	public Researches(ResearchesInfo info)
		: base(info)
	{
	}

	void INotifyAddedToWorld.AddedToWorld(Actor self)
	{
		this.research = self.Owner.PlayerActor.TraitOrDefault<Research>();
	}

	void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
	{
		this.research = newOwner.PlayerActor.TraitOrDefault<Research>();
		this.research.ConquerResearch(oldOwner);
	}

	void ITick.Tick(Actor self)
	{
		if (!this.IsTraitPaused)
			this.research?.DoResearch();
	}
}
