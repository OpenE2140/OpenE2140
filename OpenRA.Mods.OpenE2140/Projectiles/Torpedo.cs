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

using OpenRA.GameRules;
using OpenRA.Mods.Common.Projectiles;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Projectiles;

[Desc($"Modified version of {nameof(Missile)}, which makes it behave more like torpedoes in Earth 2140.")]
public class TorpedoInfo : MissileInfo
{
	public override IProjectile Create(ProjectileArgs args)
	{
		return new Torpedo(this, args);
	}
}

public class Torpedo : Missile
{
	public Torpedo(MissileInfo info, ProjectileArgs args)
		: base(info, args)
	{
	}

	protected override bool ShouldExplode(World world, MissileExplodeContext context)
	{
		// Modifies behavior of standard missile: doesn't explode, when gets close to target, if target is not (Frozen)Actor,
		// i.e. when target is terrain, missile just passes through the target cell and continues traveling.
		return
			context.HitGround ||
			((context.ProjectileArgs.GuidedTarget.Type is TargetType.Actor or TargetType.FrozenActor) && context.WithinRange) ||
			context.RanOutOfFuel ||
			context.HitIncompatibleTerrain ||
			context.AirBurst;
	}
}
