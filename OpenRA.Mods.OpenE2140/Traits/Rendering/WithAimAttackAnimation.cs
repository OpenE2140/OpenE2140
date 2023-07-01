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
using CustomAttackActivity = OpenRA.Mods.OpenE2140.Traits.Attack.AttackFrontal.Attack;

namespace OpenRA.Mods.OpenE2140.Traits.Rendering;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class WithAimAttackAnimationInfo : ConditionalTraitInfo, Requires<WithSpriteBodyInfo>, Requires<ArmamentInfo>
{
	[Desc("Armament name. If not specified, aim/attack animations are played regardless of which armament is firing.")]
	public readonly string Armament = string.Empty;

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
	private readonly Armament? armament;
	private readonly WithSpriteBody wsb;
	private bool aiming;

	public WithAimAttackAnimation(ActorInitializer init, WithAimAttackAnimationInfo info)
		: base(info)
	{
		if (!string.IsNullOrEmpty(info.Armament))
			this.armament = init.Self.TraitsImplementing<Armament>().Single(a => a.Info.Name == info.Armament);

		this.wsb = init.Self.TraitOrDefault<WithSpriteBody>();
	}

	void INotifyAttack.Attacking(Actor self, in Target target, Armament a, Barrel barrel)
	{
		// Specifying Armament (for which the aim/attack animation should be played) is optional
		// But if it is specified, it must match the Armament that is currently firing
		if (this.wsb.IsTraitDisabled || (this.armament != null && a != this.armament))
			return;

		this.wsb.PlayCustomAnimation(self, this.Info.SequenceFire);
	}

	void INotifyAttack.PreparingAttack(Actor self, in Target target, Armament a, Barrel barrel)
	{
	}

	void ITick.Tick(Actor self)
	{
		if (this.IsTraitDisabled
			|| this.wsb.IsTraitDisabled
			|| self.CurrentActivity is not CustomAttackActivity attackActivity
			|| attackActivity.IsMovingWithinRange)
			return;

		// When WithAimAttackAnimation is tied to specific armament, verify that the Attack activity is expecting to attack with this armament.
		// It's not 100% bulletproof, if the actor has multiple armaments that can attack current target (because all such armaments are ordered to fire).
		// It does work for units like Android A01, i.e. actors that have distinct armaments, that all attack different targets.
		if (this.armament != null && !attackActivity.GetExpectedArmamentsForTarget().Contains(this.armament))
			return;

		if (!string.IsNullOrEmpty(this.Info.SequenceAim) && this.aiming && this.wsb.DefaultAnimation.CurrentSequence.Name != this.Info.SequenceFire)
			this.wsb.PlayCustomAnimation(self, this.Info.SequenceAim);
	}

	void INotifyAiming.StartedAiming(Actor self, AttackBase attack)
	{
		this.aiming = true;
	}

	void INotifyAiming.StoppedAiming(Actor self, AttackBase attack)
	{
		this.aiming = false;
	}
}
