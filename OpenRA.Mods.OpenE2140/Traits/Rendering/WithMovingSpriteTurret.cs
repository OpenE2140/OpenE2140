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

namespace OpenRA.Mods.OpenE2140.Traits.Rendering;

[Desc("Renders walking unit with a turret as its upper body move up and down imposed by legs motion.")]
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class WithMovingSpriteTurretInfo : WithSpriteTurretInfo, Requires<MobileInfo>
{
	[Desc("Offset indicating how high the turret moves up.")]
	public readonly int OffsetHigh = 40;

	[Desc("Offset indicating how low the turret moves down.")]
	public readonly int OffsetLow = -40;

	[Desc("Frequency of the movement. The higher the value, the quicker turret moves.")]
	public readonly int Frequency = 20;

	[Desc("Cycle duration.")]
	public readonly int Duration = 1000;

	[Desc("Turn cycle duration.")]
	public readonly int TurnDuration = 500;

	public override object Create(ActorInitializer init) { return new WithMovingSpriteTurret(init.Self, this); }
}

public class WithMovingSpriteTurret : WithSpriteTurret, INotifyMoving
{
	private readonly Mobile mobile;
	private readonly BodyOrientation body;
	private readonly Turreted turreted;
	private readonly WithMovingSpriteTurretInfo info;

	private WVec? currentOffset;
	private int current;
	private int frequency;

	public WithMovingSpriteTurret(Actor self, WithMovingSpriteTurretInfo info)
		: base(self, info)
	{
		this.info = info;
		this.mobile = self.Trait<Mobile>();
		this.body = self.Trait<BodyOrientation>();
		this.turreted = self.TraitsImplementing<Turreted>().First(turreted => turreted.Name == this.info.Turret);
		this.frequency = info.Frequency;
	}

	private WVec CalculateTurretOffset(Actor self)
	{
		var baseOffset = base.TurretOffset(self);
		var notWalking = this.mobile.CurrentMovementTypes is MovementType.None;

		if (notWalking)
			return baseOffset;

		var duration = this.mobile.CurrentMovementTypes is MovementType.Turn ? this.info.TurnDuration : this.info.Duration;
		var interpolation = int2.Lerp(this.info.OffsetLow, this.info.OffsetHigh, this.current, duration);

		this.frequency *= this.current > duration || this.current < 0 ? -1 : 1;
		this.current += this.frequency;

		var turretMovement = new WVec(WDist.Zero, WDist.Zero, new WDist(interpolation));
		turretMovement = turretMovement.Rotate(this.turreted.WorldOrientation);
		turretMovement = this.body.LocalToWorld(turretMovement);

		return baseOffset + turretMovement;
	}

	void INotifyMoving.MovementTypeChanged(Actor self, MovementType type)
	{
		this.current = 0;
	}

	public WVec GetTurretOffset(Actor self)
	{
		return this.TurretOffset(self);
	}

	protected override WVec TurretOffset(Actor self)
	{
		if (this.currentOffset == null || !self.World.Paused)
			this.currentOffset = this.CalculateTurretOffset(self);

		return this.currentOffset.Value;
	}
}
