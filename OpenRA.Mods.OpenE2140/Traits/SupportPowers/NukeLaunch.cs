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
using OpenRA.Mods.OpenE2140.Effects;
using OpenRA.Primitives;
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

	private readonly Animation? staticTrailAnim;
	private readonly string staticTrailUp;
	private readonly string staticTrailDown;
	private readonly string staticTrailPalette;
	private readonly int staticTrailDelay;
	private readonly int staticTrailZOffset;
	private readonly int missileZOffset;
	private readonly int trailZOffset;

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
	private WPos staticTrailPos;
	private int ticks, trailTicks;
	private int launchDelay;
	private bool isLaunched;
	private bool detonated;

	public NukeLaunch(Player firedBy, string? image, WeaponInfo weapon, string weaponPalette, string upSequence, string downSequence,
		WPos launchPos, WPos targetPos, WDist detonationAltitude, bool removeOnDetonation, WDist velocity, int launchDelay, int impactDelay,
		bool skipAscent,
		string? trailImage, ImmutableArray<string> trailSequences, string trailPalette, bool trailUsePlayerPalette, int trailDelay, int trailInterval,
		bool interceptable, WeaponInfo? interceptionWeapon,
		string? staticTrailImage, string staticTrailUp, string staticTrailDown, string staticTrailPalette, bool staticTrailPaletteIsPlayerPalette,
		int staticTrailDelay, int staticTrailZOffset,
		int missileZOffset, int trailZOffset)
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

		this.staticTrailUp = staticTrailUp;
		this.staticTrailDown = staticTrailDown;
		this.staticTrailPalette = staticTrailPalette;
		if (staticTrailPaletteIsPlayerPalette)
			this.staticTrailPalette += firedBy.InternalName;

		this.staticTrailDelay = staticTrailDelay;
		this.staticTrailZOffset = staticTrailZOffset;
		this.missileZOffset = missileZOffset;
		this.trailZOffset = trailZOffset;

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

		if (!string.IsNullOrEmpty(staticTrailImage))
			this.staticTrailAnim = new Animation(firedBy.World, staticTrailImage);

		this.pos = skipAscent ? this.descendSource : this.ascendSource;
		this.staticTrailPos = this.pos;
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
				this.anim.PlayRepeating(this.upSequence);

			if (this.staticTrailAnim != null)
				this.staticTrailAnim.PlayRepeating(this.staticTrailUp);

			if (this.anim != null || this.staticTrailAnim != null)
			{
				var screenSize = this.GetCombinedScreenSize(world, this.pos, this.anim, this.staticTrailPos, this.staticTrailAnim);
				world.ScreenMap.Add(this, this.pos, screenSize);
			}

			this.isLaunched = true;
		}

		if (this.anim != null)
		{
			this.anim.Tick();

			if (this.ticks == this.turn)
				this.anim.PlayRepeating(this.downSequence);
		}

		if (this.staticTrailAnim != null)
		{
			this.staticTrailAnim.Tick();

			if (this.ticks == this.turn + this.staticTrailDelay)
				this.staticTrailAnim.PlayRepeating(this.staticTrailDown);
		}

		this.pos = this.GetPosition(this.ticks);

		if (this.staticTrailAnim != null)
			this.staticTrailPos = this.GetPosition(Math.Max(0, this.ticks - this.staticTrailDelay));

		var isDescending = this.ticks >= this.turn;
		if (!string.IsNullOrEmpty(this.trailImage) && --this.trailTicks < 0)
		{
			var trailPos = this.GetPosition(Math.Max(0, this.ticks - this.trailDelay));

			world.AddFrameEndTask(w => w.Add(new NukeTrailEffect(trailPos, w, this.trailImage, this.trailSequences.Random(world.SharedRandom),
				this.trailPalette, this.trailZOffset)));

			this.trailTicks = this.trailInterval;
		}

		var dat = world.Map.DistanceAboveTerrain(this.pos);
		if (this.ticks == this.impactDelay || (isDescending && dat <= this.detonationAltitude))
			this.Explode(world, this.ticks == this.impactDelay || this.removeOnDetonation);

		var updateScreenSize = this.GetCombinedScreenSize(world, this.pos, this.anim, this.staticTrailPos, this.staticTrailAnim);
		if (!updateScreenSize.IsEmpty)
			world.ScreenMap.Update(this, this.pos, updateScreenSize);

		this.ticks++;
	}

	private WPos GetPosition(int t)
	{
		if (t >= this.turn)
			return WPos.LerpQuadratic(this.descendSource, this.descendTarget, WAngle.Zero, t - this.turn, this.impactDelay - this.turn);

		return WPos.LerpQuadratic(this.ascendSource, this.ascendTarget, WAngle.Zero, t, this.turn);
	}

	private Size GetCombinedScreenSize(OpenRA.World world, WPos centerPos, Animation? mainAnim, WPos staticTrailPos, Animation? staticTrailAnim)
	{
		var tileSize = world.Map.Rules.TerrainInfo.TileSize;
		var tileScale = world.Map.Grid.TileScale;

		int2 ScreenPx(WPos p) => new(
			(int)Math.Round((float)tileSize.Width * p.X / tileScale),
			(int)Math.Round((float)tileSize.Height * (p.Y - p.Z) / tileScale));

		var centerScreen = ScreenPx(centerPos);
		var bounds = Rectangle.Empty;

		if (mainAnim != null)
		{
			var spriteSize = mainAnim.Image.Size;
			bounds = Rectangle.FromLTRB(
				centerScreen.X - (int)spriteSize.X / 2,
				centerScreen.Y - (int)spriteSize.Y / 2,
				centerScreen.X + (int)spriteSize.X / 2,
				centerScreen.Y + (int)spriteSize.Y / 2);
		}

		if (staticTrailAnim != null)
		{
			var staticScreen = ScreenPx(staticTrailPos);
			var spriteSize = staticTrailAnim.Image.Size;
			var rect = Rectangle.FromLTRB(
				staticScreen.X - (int)spriteSize.X / 2,
				staticScreen.Y - (int)spriteSize.Y / 2,
				staticScreen.X + (int)spriteSize.X / 2,
				staticScreen.Y + (int)spriteSize.Y / 2);
			bounds = bounds.IsEmpty ? rect : Rectangle.Union(bounds, rect);
		}

		if (bounds.IsEmpty)
			return new Size(0, 0);

		var halfWidth = Math.Max(centerScreen.X - bounds.Left, bounds.Right - centerScreen.X);
		var halfHeight = Math.Max(centerScreen.Y - bounds.Top, bounds.Bottom - centerScreen.Y);

		return new Size(2 * halfWidth, 2 * halfHeight);
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
		if (!this.isLaunched)
			return [];

		var renderables = new List<IRenderable>();
		if (this.anim != null)
			renderables.AddRange(this.anim.Render(this.pos, WVec.Zero, this.missileZOffset, wr.Palette(this.weaponPalette)));

		if (this.staticTrailAnim != null)
			renderables.AddRange(this.staticTrailAnim.Render(this.staticTrailPos, WVec.Zero, this.staticTrailZOffset, wr.Palette(this.staticTrailPalette)));

		return renderables;
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
