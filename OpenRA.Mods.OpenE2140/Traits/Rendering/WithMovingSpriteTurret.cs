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

	private int current;
	private int frequency;
	private int duration;

	public WithMovingSpriteTurret(Actor self, WithMovingSpriteTurretInfo info)
		: base(self, info)
	{
		this.info = info;
		this.mobile = self.Trait<Mobile>();
		this.body = self.Trait<BodyOrientation>();
		this.turreted = self.TraitsImplementing<Turreted>().First(turreted => turreted.Name == this.info.Turret);
		this.frequency = info.Frequency;
		this.duration = info.Duration;
	}

	public WVec GetTurretOffset(Actor self)
	{
		return this.TurretOffset(self);
	}

	protected override WVec TurretOffset(Actor self)
	{
		var offset = base.TurretOffset(self);
		var notWalking = this.mobile.CurrentMovementTypes is MovementType.None;

		if (notWalking)
			return offset;

		var interpolation = int2.Lerp(this.info.OffsetLow, this.info.OffsetHigh, this.current, this.duration);

		this.frequency *= this.current > this.duration || this.current < 0 ? -1 : 1;
		this.current += this.frequency;

		var turretMovement = new WVec(WDist.Zero, WDist.Zero, new WDist(interpolation));
		turretMovement = turretMovement.Rotate(this.turreted.WorldOrientation);
		turretMovement = this.body.LocalToWorld(turretMovement);

		return offset + turretMovement;
	}

	void INotifyMoving.MovementTypeChanged(Actor self, MovementType type)
	{
		this.current = 0;
		this.duration = type is MovementType.Turn ? this.info.TurnDuration : this.info.Duration;
	}
}
