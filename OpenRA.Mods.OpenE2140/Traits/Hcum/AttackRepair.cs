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

using OpenRA.Activities;
using OpenRA.Effects;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Hcum;

[Desc("Custom Attack trait for repair vehicle (HCU-M).")]
public class AttackRepairInfo : AttackFrontalInfo, IRenderActorPreviewSpritesInfo, Requires<MobileInfo>
{
	[Desc("Percentage value of the repair vehicle speed used when docking to target actor.")]
	public readonly int DockSpeedModifier = 30;

	[SequenceReference]
	[Desc("Displayed when idle.")]
	public readonly string IdleSequence = "turret";

	[SequenceReference]
	[Desc("Displayed while docking.")]
	public readonly string DockingSequence = "dock";

	[SequenceReference]
	[Desc("Displayed while repairing.")]
	public readonly string RepairSequence = "repair";

	[SequenceReference]
	[Desc("Displayed while undocking.")]
	public readonly string UndockingSequence = "undock";

	[Desc("Repair arm position relative to body. (forward, right, up) triples")]
	public readonly WVec ArmOffset = WVec.Zero;

	public override object Create(ActorInitializer init)
	{
		return new AttackRepair(init.Self, this);
	}

	public IEnumerable<IActorPreview> RenderPreviewSprites(ActorPreviewInitializer init, string image, int facings, PaletteReference p)
	{
		if (!this.EnabledByDefault)
			yield break;

		var body = init.Actor.TraitInfo<BodyOrientationInfo>();
		var facing = init.GetFacing();

		var anim = new Animation(init.World, image, facing);
		anim.Play(RenderSprites.NormalizeSequence(anim, init.GetDamageState(), this.IdleSequence));

		WRot Orientation()
		{
			return body.QuantizeOrientation(WRot.FromYaw(facing()), facings);
		}

		WVec Offset()
		{
			return body.LocalToWorld(this.ArmOffset.Rotate(Orientation()));
		}

		int ZOffset()
		{
			var tmpOffset = Offset();

			return -(tmpOffset.Y + tmpOffset.Z) + 1;
		}

		yield return new SpriteActorPreview(anim, Offset, ZOffset, p);
	}
}

public class AttackRepair : AttackFrontal, INotifyRepair
{
	public enum RepairState
	{
		None,
		MovingToTarget,
		DockingToTarget,
		Repairing,
		UndockingFromTarget
	}

	public new readonly AttackRepairInfo Info;

	private readonly RenderSprites rs;
	private readonly BodyOrientation body;

	public Animation DefaultAnimation { get; }

	public RepairState State { get; set; } = RepairState.None;

	public AttackRepair(Actor self, AttackRepairInfo info)
		: base(self, info)
	{
		this.Info = info;

		this.rs = self.Trait<RenderSprites>();
		this.body = self.Trait<BodyOrientation>();
		this.facing = self.Trait<IFacing>();

		this.DefaultAnimation = new Animation(self.World, this.rs.GetImage(self), () => this.body.QuantizeFacing(this.facing.Orientation.Yaw));
		this.DefaultAnimation.PlayRepeating(this.NormalizeSequence(self, this.Info.IdleSequence));
		this.rs.Add(new AnimationWithOffset(this.DefaultAnimation, () => this.TurretOffset(self), () => false));
	}

	public string NormalizeSequence(Actor self, string sequence)
	{
		return RenderSprites.NormalizeSequence(this.DefaultAnimation, self.GetDamageState(), sequence);
	}

	private WVec TurretOffset(Actor self)
	{
		var bodyOrientation = this.body.QuantizeOrientation(self.Orientation);

		return this.body.LocalToWorld(this.Info.ArmOffset.Rotate(bodyOrientation));
	}

	protected override bool CanAttack(Actor self, in Target target)
	{
		if (target.Type != TargetType.Actor)
			return false;

		if (this.State != RepairState.Repairing)
			return false;

		var mobile = target.Actor.TraitOrDefault<Mobile>();

		if (mobile != null && mobile.CurrentMovementTypes != MovementType.None)
			return false;

		var aircraft = target.Actor.TraitOrDefault<Aircraft>();

		if (aircraft != null && self.World.Map.DistanceAboveTerrain(target.CenterPosition).Length > 0)
			return false;

		return base.CanAttack(self, target);
	}

	public override Activity GetAttackActivity(
		Actor self,
		AttackSource source,
		in Target newTarget,
		bool allowMove,
		bool forceAttack,
		Color? targetLineColor = null
	)
	{
		return new RepairAttack(self, newTarget, allowMove, forceAttack, this, Color.Orange);
	}

	void INotifyRepair.Docking(Actor self, int ticksToDock)
	{
		var delay = ticksToDock - this.DefaultAnimation.GetSequence(this.Info.DockingSequence).Length - 5;
		if (delay > 0)
			self.World.Add(new DelayedAction(delay, PlayAnimation));
		else
			PlayAnimation();

		void PlayAnimation() => this.DefaultAnimation.PlayThen(this.Info.DockingSequence, () => this.DefaultAnimation.PlayRepeating(this.Info.RepairSequence));
	}

	void INotifyRepair.Repairing(Actor self)
	{
	}

	void INotifyRepair.Undocking(Actor self)
	{
		this.DefaultAnimation.PlayThen(this.Info.UndockingSequence, () => this.DefaultAnimation.Play(this.Info.IdleSequence));
	}

	void INotifyRepair.Undocked(Actor self)
	{
	}
}
