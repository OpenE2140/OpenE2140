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
using OpenRA.GameRules;
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

	[Desc("Delay in ticks before applying the warhead effect.", "0 = instant (old model).")]
	public readonly int[] Delays = { 0 };

	[Desc("The amount of projectile pieces to produce. Two values indicate a range.")]
	public readonly int[] Pieces = { 3, 10 };

	[Desc("The minimum and maximum distances the projectile may travel.")]
	public readonly WDist[] Range = { WDist.FromCells(2), WDist.FromCells(5) };

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
		var pos = new WPos(target.CenterPosition.X, target.CenterPosition.Y, Math.Max(0, target.CenterPosition.Z));
		var world = firedBy.World;

		if (!this.IsValidAgainst(target, firedBy))
			return;

		for (var i = 0; this.WeaponInfos.Length > i; i++)
		{
			var pieces = world.SharedRandom.Next(this.Pieces[0], this.Pieces[1]);
			var range = world.SharedRandom.Next(this.Range[0].Length, this.Range[1].Length);

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
						Source = pos,
						CurrentSource = () => pos,
						SourceActor = args.SourceActor,
						PassiveTarget = pos + new WVec(range, 0, 0).Rotate(rotation)
					}
				};

				if (this.UseAttackerModifiers)
				{
					projectileArgs.Args.DamageModifiers =
						firedBy.TryGetTraitsImplementing<IFirepowerModifier>().Select(a => a.GetFirepowerModifier()).ToArray();

					projectileArgs.Args.InaccuracyModifiers =
						firedBy.TryGetTraitsImplementing<IInaccuracyModifier>().Select(a => a.GetInaccuracyModifier()).ToArray();

					projectileArgs.Args.RangeModifiers = firedBy.TryGetTraitsImplementing<IRangeModifier>().Select(a => a.GetRangeModifier()).ToArray();
				}

				var delayedTarget = target;

				world.AddFrameEndTask(
					x =>
					{
						if (projectileArgs.Args.Weapon.Projectile != null)
						{
							var projectile = projectileArgs.Args.Weapon.Projectile.Create(projectileArgs.Args);

							if (projectile != null)
							{
								if (projectileArgs.Delay > 0)
									world.AddFrameEndTask(w => w.Add(new DelayedProjectile(projectile, projectileArgs.Delay)));
								else
									world.Add(projectile);
							}
						}
					}
				);
			}
		}
	}
}
