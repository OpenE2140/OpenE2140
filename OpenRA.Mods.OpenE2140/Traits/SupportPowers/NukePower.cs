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
using OpenRA.GameRules;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Effects;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.SupportPowers;

// Adapted from engine's NukePower
public class NukePowerInfo : SupportPowerInfo
{
	[WeaponReference]
	[FieldLoader.Require]
	[Desc("Weapon to use for the impact.")]
	public readonly string MissileWeapon = "";

	[Desc("Delay (in ticks) after launch until the missile is spawned.")]
	public readonly int MissileDelay = 0;

	[Desc("Image to use for the missile.")]
	public readonly string? MissileImage;

	[SequenceReference(nameof(MissileImage))]
	[Desc("Sprite sequence for the ascending missile.")]
	public readonly string MissileUp = "up";

	[SequenceReference(nameof(MissileImage))]
	[Desc("Sprite sequence for the descending missile.")]
	public readonly string MissileDown = "down";

	[Desc("Offset from the actor the missile spawns on.")]
	public readonly WVec SpawnOffset = WVec.Zero;

	[Desc("Altitude offset from the target position at which the warhead should detonate.")]
	public readonly WDist DetonationAltitude = WDist.Zero;

	[Desc("Should nuke missile projectile be removed on detonation above ground.",
		"'False' will make the missile continue until it hits the ground and disappears (without triggering another explosion).")]
	public readonly bool RemoveMissileOnDetonation = true;

	[Desc("Can the missile be intercepted by InterceptsProjectiles traits.")]
	public readonly bool Interceptable = false;

	[WeaponReference]
	[Desc("Weapon fired at the missile's position when intercepted (effects only).")]
	public readonly string? InterceptionWeapon;

	[PaletteReference(nameof(IsPlayerPalette))]
	[Desc("Palette to use for the missile weapon image.")]
	public readonly string MissilePalette = "effect";

	[Desc("Custom palette is a player palette BaseName.")]
	public readonly bool IsPlayerPalette = false;

	[Desc("Trail animation.")]
	public readonly string? TrailImage;

	[SequenceReference(nameof(TrailImage), allowNullImage: true)]
	[Desc("Loop a randomly chosen sequence of TrailImage from this list while this projectile is moving.")]
	public readonly ImmutableArray<string> TrailSequences = [];

	[Desc("Interval in ticks between each spawned Trail animation.")]
	public readonly int TrailInterval = 1;

	[Desc("Delay in ticks until trail animation is spawned.")]
	public readonly int TrailDelay = 1;

	[PaletteReference(nameof(TrailUsePlayerPalette))]
	[Desc("Palette used to render the trail sequence.")]
	public readonly string TrailPalette = "effect";

	[Desc("Use the Player Palette to render the trail sequence.")]
	public readonly bool TrailUsePlayerPalette = false;

	[Desc("Z offset for the main missile sprite. Higher values draw on top.")]
	public readonly int MissileZOffset = 0;

	[Desc("Z offset for the spawned trail SpriteEffects.")]
	public readonly int TrailZOffset = 0;

	[Desc("Image for the static trail sprite drawn between the main missile and spawned trail effects.")]
	public readonly string? StaticTrailImage;

	[SequenceReference(nameof(StaticTrailImage), allowNullImage: true)]
	[Desc("Static trail sprite sequence while ascending.")]
	public readonly string StaticTrailUp = "up";

	[SequenceReference(nameof(StaticTrailImage), allowNullImage: true)]
	[Desc("Static trail sprite sequence while descending.")]
	public readonly string StaticTrailDown = "down";

	[PaletteReference(nameof(StaticTrailPaletteIsPlayerPalette))]
	[Desc("Palette to use for the static trail sprite.")]
	public readonly string? StaticTrailPalette;

	[Desc("Use the Player Palette to render the static trail sprite.")]
	public readonly bool StaticTrailPaletteIsPlayerPalette = false;

	[Desc("Z offset for the static trail sprite. Should be between TrailZOffset and MissileZOffset.")]
	public readonly int StaticTrailZOffset = 0;

	[Desc("Delay in ticks between the main missile and the static trail position along the flight path.")]
	public readonly int StaticTrailDelay = 0;

	[Desc("Travel time - split equally between ascent and descent.")]
	public readonly int FlightDelay = 400;

	[Desc("Visual ascent velocity in WDist / tick.")]
	public readonly WDist FlightVelocity = new(512);

	[Desc("Descend immediately on the target.")]
	public readonly bool SkipAscent = false;

	[Desc("Amount of time before detonation to remove the beacon.")]
	public readonly int BeaconRemoveAdvance = 25;

	[Desc("Range of cells the camera should reveal around target cell.")]
	public readonly WDist CameraRange = WDist.Zero;

	[Desc("Can the camera reveal shroud generated by the `" + nameof(CreatesShroud) + "` trait?")]
	public readonly bool RevealGeneratedShroud = true;

	[Desc("Reveal cells to players with these relationships only.")]
	public readonly PlayerRelationship CameraRelationships = PlayerRelationship.Ally;

	[Desc("Amount of time before detonation to spawn the camera.")]
	public readonly int CameraSpawnAdvance = 25;

	[Desc("Amount of time after detonation to remove the camera.")]
	public readonly int CameraRemoveDelay = 25;

	[Desc("Range circle color.")]
	public readonly Color CircleColor = Color.FromArgb(128, Color.Red);

	[Desc("Range circle width in pixel.")]
	public readonly float CircleWidth = 1;

	[Desc("Range circle border color.")]
	public readonly Color CircleBorderColor = Color.FromArgb(64, Color.Red);

	[Desc("Range circle border width in pixel.")]
	public readonly float CircleBorderWidth = 3;

	[Desc("Render circles based on these distance ranges while targeting.")]
	public readonly ImmutableArray<WDist> CircleRanges = default;

	public WeaponInfo WeaponInfo { get; private set; } = null!;
	public WeaponInfo? InterceptionWeaponInfo { get; private set; }

	public override object Create(ActorInitializer init) { return new NukePower(init.Self, this); }
	public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
	{
		if (!string.IsNullOrEmpty(this.TrailImage) && this.TrailSequences.Length == 0)
			throw new YamlException("At least one entry in TrailSequences must be defined when TrailImage is defined.");

		var weaponToLower = (this.MissileWeapon ?? string.Empty).ToLowerInvariant();
		if (!rules.Weapons.TryGetValue(weaponToLower, out var weapon))
			throw new YamlException($"Weapons Ruleset does not contain an entry '{weaponToLower}'");

		this.WeaponInfo = weapon;

		if (!string.IsNullOrEmpty(this.InterceptionWeapon))
		{
			var interceptionWeaponToLower = this.InterceptionWeapon.ToLowerInvariant();
			if (!rules.Weapons.TryGetValue(interceptionWeaponToLower, out var interceptionWeapon))
				throw new YamlException($"Weapons Ruleset does not contain an entry '{interceptionWeaponToLower}'");

			this.InterceptionWeaponInfo = interceptionWeapon;
		}

		if (!string.IsNullOrEmpty(this.StaticTrailImage) && (string.IsNullOrEmpty(this.StaticTrailUp) || string.IsNullOrEmpty(this.StaticTrailDown)))
			throw new YamlException(
				$"When {nameof(this.StaticTrailImage)} is defined, both {nameof(this.StaticTrailUp)} and {nameof(this.StaticTrailDown)} need to be defined");

		base.RulesetLoaded(rules, ai);
	}
}

internal sealed class NukePower : SupportPower
{
	private readonly NukePowerInfo info;
	private BodyOrientation? body;

	public NukePower(Actor self, NukePowerInfo info)
		: base(self, info)
	{
		this.info = info;
	}

	protected override void Created(Actor self)
	{
		this.body = self.TraitOrDefault<BodyOrientation>();
		base.Created(self);
	}

	public override void Activate(Actor self, Order order, SupportPowerManager manager)
	{
		base.Activate(self, order, manager);
		this.PlayLaunchSounds();

		this.Activate(self, order.Target.CenterPosition);
	}

	public void Activate(Actor self, WPos targetPosition)
	{
		var palette = this.info.IsPlayerPalette ? this.info.MissilePalette + self.Owner.InternalName : this.info.MissilePalette;
		var skipAscent = this.info.SkipAscent || this.body == null;
		var launchPos = skipAscent ? WPos.Zero : self.CenterPosition + this.body?.LocalToWorld(this.info.SpawnOffset) ?? WPos.Zero;

		var missile = new NukeLaunch(self.Owner, this.info.MissileImage, this.info.WeaponInfo, palette, this.info.MissileUp, this.info.MissileDown,
			launchPos,
			targetPosition, this.info.DetonationAltitude, this.info.RemoveMissileOnDetonation,
			this.info.FlightVelocity, this.info.MissileDelay, this.info.FlightDelay, skipAscent,
			this.info.TrailImage, this.info.TrailSequences, this.info.TrailPalette, this.info.TrailUsePlayerPalette, this.info.TrailDelay, this.info.TrailInterval,
			this.info.Interceptable, this.info.InterceptionWeaponInfo,
			this.info.StaticTrailImage, this.info.StaticTrailUp, this.info.StaticTrailDown, this.info.StaticTrailPalette, this.info.StaticTrailPaletteIsPlayerPalette,
			this.info.StaticTrailDelay, this.info.StaticTrailZOffset,
			this.info.MissileZOffset, this.info.TrailZOffset);

		self.World.AddFrameEndTask(w => w.Add(missile));

		if (this.info.CameraRange != WDist.Zero)
		{
			var type = this.info.RevealGeneratedShroud ? Shroud.SourceType.Visibility : Shroud.SourceType.PassiveVisibility;

			self.World.AddFrameEndTask(w => w.Add(new RevealShroudEffect(targetPosition, this.info.CameraRange, type, self.Owner, this.info.CameraRelationships,
				this.info.FlightDelay - this.info.CameraSpawnAdvance, this.info.CameraSpawnAdvance + this.info.CameraRemoveDelay)));
		}

		if (this.Info.DisplayBeacon)
		{
			var beacon = new Beacon(
				self.Owner,
				targetPosition,
				this.Info.BeaconPaletteIsPlayerPalette,
				this.Info.BeaconPalette,
				this.Info.BeaconImage,
				this.Info.BeaconPoster,
				this.Info.BeaconPosterPalette,
				this.Info.BeaconSequence,
				this.Info.ArrowSequence,
				this.Info.CircleSequence,
				this.Info.ClockSequence,
				() => missile.FractionComplete,
				this.Info.BeaconDelay,
				this.info.FlightDelay - this.info.BeaconRemoveAdvance);

			self.World.AddFrameEndTask(w => w.Add(beacon));
		}
	}

	public override void SelectTarget(Actor self, string order, SupportPowerManager manager)
	{
		self.World.OrderGenerator = new SelectNukePowerTarget(order, manager, this.info);
	}
}

public class SelectNukePowerTarget : SelectGenericPowerTarget
{
	private readonly NukePowerInfo info;

	public SelectNukePowerTarget(string order, SupportPowerManager manager, NukePowerInfo info)
		: base(order, manager, info)
	{
		this.info = info;
	}

	protected override IEnumerable<IRenderable> RenderAnnotations(WorldRenderer wr, OpenRA.World world)
	{
		if (this.info.CircleRanges == null)
			yield break;

		var centerPosition = wr.World.Map.CenterOfCell(wr.Viewport.ViewToWorld(Viewport.LastMousePos));
		foreach (var range in this.info.CircleRanges)
			yield return new RangeCircleAnnotationRenderable(
				centerPosition,
				range,
				0,
				this.info.CircleColor,
				this.info.CircleWidth,
				this.info.CircleBorderColor,
				this.info.CircleBorderWidth);
	}
}
