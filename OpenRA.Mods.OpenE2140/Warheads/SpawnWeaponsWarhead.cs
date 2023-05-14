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
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Warheads;
using OpenRA.Mods.OpenE2140.Effects;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Warheads;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("Spawn a weapon(s) on warhead impact.")]
public class SpawnWeaponsWarhead : Warhead, IRulesetLoaded<WeaponInfo>
{
	[Desc("The weapons to spawn.")]
	public readonly string[] Weapons = Array.Empty<string>();

	[Desc("Delay in ticks before applying the warhead effect.", "0 = instant (old model).")]
	public readonly int[] Delays = new int[] { 0 };

	[Desc("The amount of projectile pieces to produce. Two values indicate a range.")]
	public readonly int[] Pieces = { 3, 10 };

	[Desc("The minimum and maximum distances the projectile may travel.")]
	public readonly WDist[] Range = { WDist.FromCells(2), WDist.FromCells(5) };

	[Desc("Whether to consider actors in determining whether the impact should happen. If false, only terrain will be considered.")]
	public readonly bool ImpactActors = true;

	internal WeaponInfo[] WeaponInfos { get; private set; } = Array.Empty<WeaponInfo>();

	public void RulesetLoaded(Ruleset rules, WeaponInfo info)
	{
		if (this.Weapons.Length != this.Delays.Length)
			throw new YamlException($"Length of: '{nameof(this.Weapons)}' and '{nameof(this.Delays)}' must be equal.");

		this.WeaponInfos = this.Weapons.Select(w =>
		{
			var weaponToLower = w.ToLowerInvariant();
			if (!rules.Weapons.TryGetValue(weaponToLower, out var weapon))
				throw new YamlException($"Weapons Ruleset does not contain an entry '{weaponToLower}'");
			return weapon;
		}).ToArray();
	}

	public override void DoImpact(in Target target, WarheadArgs args)
	{
		if (target.Type == TargetType.Invalid)
			return;

		var firedBy = args.SourceActor;
		var pos = new WPos(target.CenterPosition.X, target.CenterPosition.Y, Math.Max(0, target.CenterPosition.Z));
		var world = firedBy.World;

		var actorAtImpact = this.ImpactActors ? this.ActorTypeAtImpact(world, pos, firedBy) : ImpactActorType.None;

		// Ignore the impact if there are only invalid actors within range
		if (actorAtImpact == ImpactActorType.Invalid)
			return;

		// Ignore the impact if there are no valid actors and no valid terrain
		// (impacts are allowed on valid actors sitting on invalid terrain!)
		if (actorAtImpact == ImpactActorType.None && !this.IsValidAgainstTerrain(world, pos))
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
						DamageModifiers = firedBy.TraitsImplementing<IFirepowerModifier>().Select(a => a.GetFirepowerModifier()).ToArray(),
						InaccuracyModifiers = firedBy.TraitsImplementing<IInaccuracyModifier>().Select(a => a.GetInaccuracyModifier()).ToArray(),
						RangeModifiers = firedBy.TraitsImplementing<IRangeModifier>().Select(a => a.GetRangeModifier()).ToArray(),
						Source = pos,
						CurrentSource = () => pos,
						SourceActor = args.SourceActor,
						PassiveTarget = pos + new WVec(range, 0, 0).Rotate(rotation)
					}
				};
				var delayedTarget = target;
				world.AddFrameEndTask(x =>
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
				});
			}
		}
	}

	private ImpactActorType ActorTypeAtImpact(World world, WPos pos, Actor firedBy)
	{
		var anyInvalidActor = false;

		// Check whether the impact position overlaps with an actor's hitshape
		foreach (var victim in world.FindActorsOnCircle(pos, WDist.Zero))
		{
			if (!this.AffectsParent && victim == firedBy)
				continue;

			var activeShapes = victim.TraitsImplementing<HitShape>().Where(t => !t.IsTraitDisabled);
			if (!activeShapes.Any(s => s.DistanceFromEdge(victim, pos).Length <= 0))
				continue;

			if (this.IsValidAgainst(victim, firedBy))
				return ImpactActorType.Valid;

			anyInvalidActor = true;
		}

		return anyInvalidActor ? ImpactActorType.Invalid : ImpactActorType.None;
	}

	private bool IsValidAgainstTerrain(World world, WPos pos)
	{
		BitSet<TargetableType> targetTypeAir = new("Air");
		var cell = world.Map.CellContaining(pos);
		if (!world.Map.Contains(cell))
			return false;

		var dat = world.Map.DistanceAboveTerrain(pos);
		return this.IsValidTarget(dat > this.AirThreshold ? targetTypeAir : world.Map.GetTerrainInfo(cell).TargetTypes);
	}

}
