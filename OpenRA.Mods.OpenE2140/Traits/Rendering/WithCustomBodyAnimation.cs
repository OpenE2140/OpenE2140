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

[Desc("Plays custom animation while the trait is enabled/unpaused.")]
public class WithCustomBodyAnimationInfo : ConditionalTraitInfo, Requires<WithSpriteBodyInfo>
{
	[SequenceReference]
	[Desc("Sequence to play while active.")]
	public readonly string Sequence = string.Empty;

	[Desc("Which sprite body to modify.")]
	public readonly string Body = "body";

	public override object Create(ActorInitializer init)
	{
		return new WithCustomBodyAnimation(init, this);
	}
}

public class WithCustomBodyAnimation : ConditionalTrait<WithCustomBodyAnimationInfo>
{
	private readonly WithSpriteBody wsb;

	public WithCustomBodyAnimation(ActorInitializer init, WithCustomBodyAnimationInfo info)
		: base(info)
	{
		this.wsb = init.Self.TraitsImplementing<WithSpriteBody>().Single(w => w.Info.Name == this.Info.Body);
	}

	protected override void TraitEnabled(Actor self)
	{
		if (!string.IsNullOrEmpty(this.Info.Sequence))
			this.wsb.PlayCustomAnimationRepeating(self, this.Info.Sequence);
	}

	protected override void TraitDisabled(Actor self)
	{
		if (!string.IsNullOrEmpty(this.Info.Sequence))
			this.wsb.CancelCustomAnimation(self);
	}
}
