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
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Rendering;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class WithAimAttackAnimationInfo : ConditionalTraitInfo, Requires<WithSpriteBodyInfo>, Requires<ArmamentInfo>
{
	[Desc("Armament name")]
	public readonly string Armament = "primary";

	[Desc("Displayed while attacking.")]
	[SequenceReference]
	public readonly string SequenceFire = string.Empty;

	[Desc("Displayed while aiming.")]
	[SequenceReference]
	public readonly string SequenceAim = string.Empty;

	public override object Create(ActorInitializer init)
	{
		return new WithAimAttackAnimation(init, this);
	}
}

public class WithAimAttackAnimation : ConditionalTrait<WithAimAttackAnimationInfo>, ITick, INotifyAttack, INotifyAiming
{
	private readonly Armament armament;
	private readonly WithSpriteBody wsb;
	private bool aiming;
	private bool isAttacking;

	public WithAimAttackAnimation(ActorInitializer init, WithAimAttackAnimationInfo info)
		: base(info)
	{
		this.armament = init.Self.TraitsImplementing<Armament>()
			.Single(a => a.Info.Name == info.Armament);
		this.wsb = init.Self.TraitOrDefault<WithSpriteBody>();
	}

	void INotifyAttack.Attacking(Actor self, in Target target, Armament a, Barrel barrel)
	{
		if (wsb.IsTraitDisabled || a != this.armament)
		{
			this.isAttacking = false;
			return;
		}

		this.isAttacking = true;
		this.wsb.PlayCustomAnimation(self, this.Info.SequenceFire);
	}

	void INotifyAttack.PreparingAttack(Actor self, in Target target, Armament a, Barrel barrel)
	{
	}

	void ITick.Tick(Actor self)
	{
		if (this.IsTraitDisabled || wsb.IsTraitDisabled || !this.isAttacking)
			return;

		if (!string.IsNullOrEmpty(this.Info.SequenceAim) && this.aiming && this.wsb.DefaultAnimation.CurrentSequence.Name == "idle")
			this.wsb.PlayCustomAnimation(self, this.Info.SequenceAim);
	}

	void INotifyAiming.StartedAiming(Actor self, AttackBase attack)
	{
		this.aiming = true;
	}

	void INotifyAiming.StoppedAiming(Actor self, AttackBase attack)
	{
		this.aiming = false;
		this.isAttacking = false;
	}
}
