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

namespace OpenRA.Mods.OpenE2140.Traits.Rendering;

public class WithPoweredSpriteBodyInfo : WithSpriteBodyInfo
{
	public readonly string NoPowerSuffix = "-nopower";

	public override object Create(ActorInitializer init)
	{
		return new WithPoweredSpriteBody(init, this);
	}
}

public class WithPoweredSpriteBody : WithSpriteBody, INotifyPowerLevelChanged
{
	private readonly WithPoweredSpriteBodyInfo info;

	public WithPoweredSpriteBody(ActorInitializer init, WithPoweredSpriteBodyInfo info)
		: base(init, info)
	{
		this.info = info;
	}

	private string GetSequence(Actor self)
	{
		var powerManager = self.Owner.PlayerActor.TraitOrDefault<PowerManager>();

		return powerManager == null || powerManager.PowerState == PowerState.Normal ? this.info.Sequence : this.info.Sequence + this.info.NoPowerSuffix;
	}

	void INotifyPowerLevelChanged.PowerLevelChanged(Actor self)
	{
		if (!this.IsTraitDisabled)
			this.DefaultAnimation.Play(this.NormalizeSequence(self, this.GetSequence(self)));
	}

	protected override void DamageStateChanged(Actor self)
	{
		if (!this.IsTraitDisabled)
			this.DefaultAnimation.Play(this.NormalizeSequence(self, this.GetSequence(self)));
	}

	protected override void TraitEnabled(Actor self)
	{
		base.TraitEnabled(self);
		this.DefaultAnimation.Play(this.NormalizeSequence(self, this.GetSequence(self)));
	}
}
