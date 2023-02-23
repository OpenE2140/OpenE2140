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
	public readonly int OffsetLow = -30;

	[Desc("Frequency of the movement. The higher the value, the quicker turret moves.")]
	public readonly int Frequency = 20;

	[Desc("Cycle duration.")]
	public readonly int Duration = 1000;

	public override object Create(ActorInitializer init) { return new WithMovingSpriteTurret(init.Self, this); }
}

public class WithMovingSpriteTurret : WithSpriteTurret
{
	private readonly Mobile mobile;
	private readonly BodyOrientation body;
	private readonly Turreted turreted;
	private readonly WithMovingSpriteTurretInfo info;

	private int current;
	private int frequency;

	public WithMovingSpriteTurret(Actor self, WithMovingSpriteTurretInfo info) : base(self, info)
	{
		this.info = info;
		this.mobile = self.Trait<Mobile>();
		this.body = self.Trait<BodyOrientation>();
		this.turreted = self.TraitsImplementing<Turreted>().First(turreted  => turreted.Name == this.info.Turret);
		this.frequency = info.Frequency;
	}

	protected override WVec TurretOffset(Actor self)
	{
		var offset = base.TurretOffset(self);
		var notWalking = this.mobile.CurrentMovementTypes is MovementType.None or MovementType.Turn;

		if (notWalking)
			return offset;

		var interpolation = int2.Lerp(this.info.OffsetLow, this.info.OffsetHigh, this.current, this.info.Duration);

		this.frequency *= this.current > this.info.Duration || this.current < 0 ? -1 : 1;
		this.current += this.frequency;

		var turretMovement = new WVec(WDist.Zero, WDist.Zero, new WDist(interpolation));
		turretMovement = turretMovement.Rotate(this.turreted.WorldOrientation);
		turretMovement = this.body.LocalToWorld(turretMovement);

		return offset + turretMovement;
	}
}
