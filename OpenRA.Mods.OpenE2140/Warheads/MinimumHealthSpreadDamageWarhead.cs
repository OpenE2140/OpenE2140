using OpenRA.GameRules;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Warheads;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Warheads;

[Desc("Apply damage that is limited so target's health don't fall below a threshold.")]
public class MinimumHealthSpreadDamageWarhead : SpreadDamageWarhead
{
	[Desc($"Minimum health of victim below which this warhead won't do any damage to its victims.")]
	public readonly int MinimumHealth = 0;

	protected override void InflictDamage(Actor victim, Actor firedBy, HitShape shape, WarheadArgs args)
	{
		var damage = Util.ApplyPercentageModifiers(this.Damage, args.DamageModifiers.Append(this.DamageVersus(victim, shape, args)));

		damage = Math.Min(damage, victim.Trait<Health>().HP - this.MinimumHealth);

		if (damage > 0)
			victim.InflictDamage(firedBy, new Damage(damage, this.DamageTypes));
	}
}
