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

using System.Collections.Immutable;
using OpenRA.Effects;
using OpenRA.GameRules;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Effects;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.SupportPowers;

// Adapted from engine's NukeLaunch
public class NukeLaunch : IProjectile, ISpatiallyPartitionable, IInterceptableProjectile
{
	private readonly Player firedBy;
	private readonly Animation? anim;
	private readonly WeaponInfo weapon;
	private readonly string weaponPalette;
	private readonly string upSequence;
	private readonly string downSequence;

	private readonly WPos ascendSource;
	private readonly WPos ascendTarget;
	private readonly WPos descendSource;
	private readonly WPos descendTarget;
	private readonly WDist detonationAltitude;
	private readonly bool removeOnDetonation;
	private readonly bool interceptable;
	private readonly WeaponInfo? interceptionWeapon;
	private readonly int impactDelay;
	private readonly int turn;
	private readonly string? trailImage;
	private readonly ImmutableArray<string> trailSequences;
	private readonly string trailPalette;
	private readonly int trailInterval;
	private readonly int trailDelay;

	private WPos pos;
	private int ticks, trailTicks;
	private int launchDelay;
	private bool isLaunched;
	private bool detonated;

	public NukeLaunch(Player firedBy, string? image, WeaponInfo weapon, string weaponPalette, string upSequence, string downSequence,
		WPos launchPos, WPos targetPos, WDist detonationAltitude, bool removeOnDetonation, WDist velocity, int launchDelay, int impactDelay,
		bool skipAscent,
		string? trailImage, ImmutableArray<string> trailSequences, string trailPalette, bool trailUsePlayerPalette, int trailDelay, int trailInterval,
		bool interceptable, WeaponInfo? interceptionWeapon)
	{
		this.firedBy = firedBy;
		this.weapon = weapon;
		this.weaponPalette = weaponPalette;
		this.upSequence = upSequence;
		this.downSequence = downSequence;
		this.launchDelay = launchDelay;
		this.impactDelay = impactDelay;
		this.turn = skipAscent ? 0 : impactDelay / 2;
		this.trailImage = trailImage;
		this.trailSequences = trailSequences;
		this.trailPalette = trailPalette;
		if (trailUsePlayerPalette)
			this.trailPalette += firedBy.InternalName;

		this.trailInterval = trailInterval;
		this.trailDelay = trailDelay;
		this.trailTicks = trailDelay;

		var offset = new WVec(WDist.Zero, WDist.Zero, velocity * (impactDelay - this.turn));
		this.ascendSource = launchPos;
		this.ascendTarget = launchPos + offset;
		this.descendSource = targetPos + offset;
		this.descendTarget = targetPos;
		this.detonationAltitude = detonationAltitude;
		this.removeOnDetonation = removeOnDetonation;
		this.interceptable = interceptable;
		this.interceptionWeapon = interceptionWeapon;

		if (!string.IsNullOrEmpty(image))
			this.anim = new Animation(firedBy.World, image);

		this.pos = skipAscent ? this.descendSource : this.ascendSource;
	}

	public void Tick(OpenRA.World world)
	{
		if (this.launchDelay-- > 0)
			return;

		if (!this.isLaunched)
		{
			if (this.weapon.Report != null && this.weapon.Report.Length > 0)
				Game.Sound.Play(SoundType.World, this.weapon.Report, world, this.pos);

			if (this.anim != null)
			{
				this.anim.PlayRepeating(this.upSequence);
				world.ScreenMap.Add(this, this.pos, this.anim.Image);
			}

			this.isLaunched = true;
		}

		if (this.anim != null)
		{
			this.anim.Tick();

			if (this.ticks == this.turn)
				this.anim.PlayRepeating(this.downSequence);
		}

		var isDescending = this.ticks >= this.turn;
		if (!isDescending)
			this.pos = WPos.LerpQuadratic(this.ascendSource, this.ascendTarget, WAngle.Zero, this.ticks, this.turn);
		else
			this.pos = WPos.LerpQuadratic(this.descendSource, this.descendTarget, WAngle.Zero, this.ticks - this.turn, this.impactDelay - this.turn);

		if (!string.IsNullOrEmpty(this.trailImage) && --this.trailTicks < 0)
		{
			var trailPos = !isDescending ? WPos.LerpQuadratic(this.ascendSource, this.ascendTarget, WAngle.Zero, this.ticks - this.trailDelay, this.turn)
				: WPos.LerpQuadratic(this.descendSource, this.descendTarget, WAngle.Zero, this.ticks - this.turn - this.trailDelay, this.impactDelay - this.turn);

			world.AddFrameEndTask(w => w.Add(new SpriteEffect(trailPos, w, this.trailImage, this.trailSequences.Random(world.SharedRandom),
				this.trailPalette)));

			this.trailTicks = this.trailInterval;
		}

		var dat = world.Map.DistanceAboveTerrain(this.pos);
		if (this.ticks == this.impactDelay || (isDescending && dat <= this.detonationAltitude))
			this.Explode(world, this.ticks == this.impactDelay || this.removeOnDetonation);

		if (this.anim != null)
			world.ScreenMap.Update(this, this.pos, this.anim.Image);

		this.ticks++;
	}

	private void Explode(OpenRA.World world, bool removeProjectile)
	{
		if (removeProjectile)
			world.AddFrameEndTask(w => { w.Remove(this); w.ScreenMap.Remove(this); });

		if (this.detonated)
			return;

		var target = Target.FromPos(this.pos);
		var warheadArgs = new WarheadArgs
		{
			Weapon = this.weapon,
			Source = target.CenterPosition,
			SourceActor = this.firedBy.PlayerActor,
			WeaponTarget = target
		};

		this.weapon.Impact(target, warheadArgs);

		this.detonated = true;
	}

	public IEnumerable<IRenderable> Render(WorldRenderer wr)
	{
		if (!this.isLaunched || this.anim == null)
			return [];

		return this.anim.Render(this.pos, wr.Palette(this.weaponPalette));
	}

	public float FractionComplete => this.ticks * 1f / this.impactDelay;

	WPos IInterceptableProjectile.Position => this.pos;

	Player IInterceptableProjectile.Owner => this.firedBy;

	void IInterceptableProjectile.Intercept(OpenRA.World world, Actor interceptor)
	{
		if (!this.interceptable)
			return;

		world.AddFrameEndTask(w => { w.Remove(this); w.ScreenMap.Remove(this); });

		if (this.detonated)
			return;

		if (this.interceptionWeapon != null)
		{
			var target = Target.FromPos(this.pos);
			var warheadArgs = new WarheadArgs
			{
				Weapon = this.interceptionWeapon,
				Source = target.CenterPosition,
				SourceActor = this.firedBy.PlayerActor,
				WeaponTarget = target
			};

			this.interceptionWeapon.Impact(target, warheadArgs);
		}

		this.detonated = true;
	}
}
