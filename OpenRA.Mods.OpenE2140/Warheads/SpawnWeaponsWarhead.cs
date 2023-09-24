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
using OpenRA.Effects;
using OpenRA.GameRules;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Effects;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Warheads;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("Spawn a weapon(s) on warhead impact.")]
public class SpawnWeaponsWarhead : EffectWarhead, IRulesetLoaded<WeaponInfo>
{
	[Desc("The weapons to spawn.")]
	public readonly string[] Weapons = Array.Empty<string>();

	[Desc("Delay in ticks before applying the warhead effect.", "0 = instant.")]
	public readonly int[] Delays = { 0 };

	[Desc("The amount of projectile pieces to produce.")]
	public readonly int[] Pieces = { 1 };

	[Desc("The minimum and maximum distances the projectile may travel.")]
	public readonly WDist[] Range = { WDist.FromCells(2), WDist.FromCells(5) };

	[Desc("The maximum inaccuracy of the effect spawn position relative to actual impact position for each weapon.")]
	public readonly WDist[] Inaccuracies = { WDist.Zero };

	[Desc(
		"Whether to use Damage, Inaccuracy and Range modifiers from source actor. If the source actor does not exist in the world, "
		+ "the modifiers are not applied anyway."
	)]
	public readonly bool UseAttackerModifiers = true;

	internal WeaponInfo[] WeaponInfos { get; private set; } = Array.Empty<WeaponInfo>();

	public void RulesetLoaded(Ruleset rules, WeaponInfo info)
	{
		if (this.Weapons.Length != this.Delays.Length)
			throw new YamlException($"Length of: '{nameof(this.Weapons)}' and '{nameof(this.Delays)}' must be equal.");

		if (this.Inaccuracies.Length > 1 && this.Inaccuracies.Length < this.Weapons.Length)
			throw new YamlException($"Length of: '{nameof(this.Inaccuracies)}' must be equal to '{nameof(this.Weapons)}' or default single value.");

		this.WeaponInfos = this.Weapons.Select(
				w =>
				{
					var weaponToLower = w.ToLowerInvariant();

					if (!rules.Weapons.TryGetValue(weaponToLower, out var weapon))
						throw new YamlException($"Weapons Ruleset does not contain an entry '{weaponToLower}'");

					return weapon;
				}
			)
			.ToArray();
	}

	public override void DoImpact(in Target target, WarheadArgs args)
	{
		var firedBy = args.SourceActor;
		var actorCenterPosition = new WPos(target.CenterPosition.X, target.CenterPosition.Y, Math.Max(0, target.CenterPosition.Z));
		var world = firedBy.World;
		var inaccuracies = this.Inaccuracies.Length > 1 ? this.Inaccuracies : Enumerable.Repeat(this.Inaccuracies[0], this.WeaponInfos.Length).ToArray();

		if (!this.IsValidAgainst(target, firedBy))
			return;

		for (var i = 0; this.WeaponInfos.Length > i; i++)
		{
			var pieces = Util.RandomInRange(world.SharedRandom, this.Pieces);
			var range = world.SharedRandom.Next(this.Range[0].Length, this.Range[1].Length);
			var inaccuracy = inaccuracies[i];

			var weaponImpactPosition = actorCenterPosition + WVec.FromPDF(world.SharedRandom, 2) * inaccuracy.Length / 1024;

			for (var j = 0; pieces > j; j++)
			{
				var rotation = WRot.FromYaw(new WAngle(world.SharedRandom.Next(1024)));

				var projectileArgs = new
				{
					Delay = this.Delays[i],
					Args = new ProjectileArgs
					{
						Weapon = this.WeaponInfos[i],
						Facing = new WAngle(world.SharedRandom.Next(1024)),
						CurrentMuzzleFacing = () => WAngle.Zero,
						Source = weaponImpactPosition,
						CurrentSource = () => weaponImpactPosition,
						SourceActor = args.SourceActor,
						PassiveTarget = weaponImpactPosition + new WVec(range, 0, 0).Rotate(rotation)
					}
				};

				if (this.UseAttackerModifiers && firedBy.IsInWorld)
				{
					projectileArgs.Args.DamageModifiers =
						firedBy.TryGetTraitsImplementing<IFirepowerModifier>().Select(a => a.GetFirepowerModifier()).ToArray();

					projectileArgs.Args.InaccuracyModifiers =
						firedBy.TryGetTraitsImplementing<IInaccuracyModifier>().Select(a => a.GetInaccuracyModifier()).ToArray();

					projectileArgs.Args.RangeModifiers = firedBy.TryGetTraitsImplementing<IRangeModifier>().Select(a => a.GetRangeModifier()).ToArray();
				}

				var positionedTarget = Target.FromPos(weaponImpactPosition);

				world.AddFrameEndTask(
					_ =>
					{
						if (projectileArgs.Args.Weapon.Projectile is null)
						{
							var warheadArgs = new WarheadArgs(projectileArgs.Args);

							foreach (var warhead in projectileArgs.Args.Weapon.Warheads)
							{
								if (projectileArgs.Delay > 0)
									world.AddFrameEndTask(w => w.Add(new DelayedImpact(projectileArgs.Delay, warhead, positionedTarget, warheadArgs)));
								else
									warhead.DoImpact(positionedTarget, warheadArgs);
							}
						}
						else
						{
							var projectile = projectileArgs.Args.Weapon.Projectile.Create(projectileArgs.Args);

							if (projectileArgs.Delay > 0)
								world.AddFrameEndTask(w => w.Add(new DelayedProjectile(projectile, projectileArgs.Delay)));
							else
								world.Add(projectile);
						}
					}
				);
			}
		}
	}
}
