#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using BaseAttackFrontalInfo = OpenRA.Mods.Common.Traits.AttackFrontalInfo;
using BaseAttackFrontal = OpenRA.Mods.Common.Traits.AttackFrontal;
using BaseAttack  = OpenRA.Mods.Common.Activities.Attack;
using OpenRA.Traits;
using OpenRA.Primitives;
using OpenRA.Mods.Common.Traits;
using OpenRA.Activities;
using OpenRA.Mods.Common.Activities;

namespace OpenRA.Mods.OpenE2140.Traits.Attack;

[Desc("Unit got to face the target. ",
	$"Works same as ORA's {nameof(AttackFrontal)} just the {nameof(AttackFrontal.Attack)} activity exposes more information.")]
public class AttackFrontalInfo : BaseAttackFrontalInfo, Requires<IFacingInfo>
{
	public override object Create(ActorInitializer init) { return new AttackFrontal(init.Self, this); }
}

public class AttackFrontal : BaseAttackFrontal
{
	public AttackFrontal(Actor self, AttackFrontalInfo info)
		: base(self, info)
	{
	}

	public override Activity GetAttackActivity(Actor self, AttackSource source, in Target newTarget, bool allowMove, bool forceAttack, Color? targetLineColor = null)
	{
		return new Attack(self, newTarget, allowMove, forceAttack, targetLineColor);
	}

	public class Attack : BaseAttack
	{
		private readonly IEnumerable<AttackFrontal> attackFrontalTraits;
		private readonly bool forceAttack;

		public bool IsMovingWithinRange => this.ChildActivity is MoveWithinRange;

		public Attack(Actor self, in Target target, bool allowMovement, bool forceAttack, Color? targetLineColor = null)
			: base(self, target, allowMovement, forceAttack, targetLineColor)
		{
			this.attackFrontalTraits = self.TraitsImplementing<AttackFrontal>().ToArray().Where(t => !t.IsTraitDisabled);
			this.forceAttack = forceAttack;
		}

		public IEnumerable<Armament> GetExpectedArmamentsForTarget(AttackFrontal? attackBase = null)
		{
			if (attackBase != null)
			{
				if (!this.attackFrontalTraits.Contains(attackBase))
					throw new ArgumentException($"Unknown {nameof(AttackFrontal)} was specified for GetExpectedArmamentsForTarget",
						nameof(attackBase));

				return attackBase.ChooseArmamentsForTarget(this.target, this.forceAttack);
			}

			return this.GetExpectedArmamentsForTargetCore();
		}

		private IEnumerable<Armament> GetExpectedArmamentsForTargetCore()
		{
			foreach (var ab in this.attackFrontalTraits)
			{
				var armaments = ab.ChooseArmamentsForTarget(this.target, this.forceAttack);

				var gotArmaments = false;
				foreach (var armament in armaments)
				{
					gotArmaments = true;
					yield return armament;
				}

				if (gotArmaments)
					yield break;
			}
		}
	}
}
