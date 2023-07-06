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
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Rendering;

public class WithDeployMineAnimationInfo : ConditionalTraitInfo, Requires<WithSpriteBodyInfo>, Requires<MinelayerInfo>
{
	[SequenceReference]
	[Desc("Displayed while laying mine.")]
	public readonly string Sequence = string.Empty;

	[Desc("Which sprite body to modify.")]
	public readonly string Body = "body";

	public override object Create(ActorInitializer init)
	{
		return new WithDeployMineAnimation(init, this);
	}
}

public class WithDeployMineAnimation : ConditionalTrait<WithDeployMineAnimationInfo>, INotifyMineLaying
{
	private readonly WithSpriteBody wsb;

	public WithDeployMineAnimation(ActorInitializer init, WithDeployMineAnimationInfo info)
		: base(info)
	{
		this.wsb = init.Self.TraitsImplementing<WithSpriteBody>().Single(w => w.Info.Name == this.Info.Body);
	}

	void INotifyMineLaying.MineLaid(Actor self, Actor mine)
	{
		this.wsb.CancelCustomAnimation(self);
	}

	void INotifyMineLaying.MineLaying(Actor self, CPos location)
	{
		if (!this.IsTraitDisabled && !this.wsb.IsTraitDisabled && !string.IsNullOrEmpty(this.Info.Sequence))
			this.wsb.PlayCustomAnimationRepeating(self, this.Info.Sequence);
	}

	void INotifyMineLaying.MineLayingCanceled(Actor self, CPos location)
	{
		this.wsb.CancelCustomAnimation(self);
	}
}
