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
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;
using static OpenRA.Mods.OpenE2140.Traits.Hcum.AttackRepair;

namespace OpenRA.Mods.OpenE2140.Traits.Hcum;

public class RepairAttack : Activity, IActivityNotifyStanceChanged
{
	private static readonly CVec[] AllowedDockDirections = { new CVec(-1, 0), new CVec(0, -1), new CVec(0, 1), new CVec(1, 0) };

	private readonly AttackRepair attack;
	private readonly Mobile mobile;
	private readonly INotifyRepair[] notifyRepair;
	private readonly bool allowMovement;
	private readonly bool forceAttack;
	private readonly Color? targetLineColor;
	private Target target;
	private Target lastVisibleTarget;
	private bool useLastVisibleTarget;
	private WDist lastVisibleMinRange;
	private WDist lastVisibleMaxRange;
	private BitSet<TargetableType> lastVisibleTargetTypes;
	private Player? lastVisibleOwner;

	private RepairState RepairState
	{
		get => this.attack.State;
		set => this.attack.State = value;
	}

	public RepairAttack(Actor self, Target target, bool allowMovement, bool forceAttack, AttackRepair attackRepair, Color? targetLineColor)
	{
		this.target = target;
		this.allowMovement = allowMovement;
		this.forceAttack = forceAttack;
		this.attack = attackRepair;
		this.targetLineColor = targetLineColor;

		this.mobile = self.Trait<Mobile>();
		this.notifyRepair = self.TraitsImplementing<INotifyRepair>().ToArray();

		// The target may become hidden between the initial order request and the first tick (e.g. if queued)
		// Moving to any position (even if quite stale) is still better than immediately giving up
		if ((target.Type == TargetType.Actor && target.Actor.CanBeViewedByPlayer(self.Owner))
			|| target.Type == TargetType.FrozenActor
			|| target.Type == TargetType.Terrain)
		{
			this.lastVisibleTarget = Target.FromPos(target.CenterPosition);
			this.lastVisibleMinRange = this.attack.GetMinimumRangeVersusTarget(target);
			this.lastVisibleMaxRange = this.attack.GetMaximumRangeVersusTarget(target);

			if (target.Type == TargetType.Actor)
			{
				this.lastVisibleOwner = target.Actor.Owner;
				this.lastVisibleTargetTypes = target.Actor.GetEnabledTargetTypes();
			}
			else if (target.Type == TargetType.FrozenActor)
			{
				this.lastVisibleOwner = target.FrozenActor.Owner;
				this.lastVisibleTargetTypes = target.FrozenActor.TargetTypes;
			}
		}
	}

	protected override void OnFirstRun(Actor self)
	{
		this.attack.IsAiming = true;
		this.attack.State = RepairState.None;
	}

	public override bool Tick(Actor self)
	{
		if (this.attack.IsTraitPaused || this.attack.IsTraitDisabled)
		{
			// HACK: When this HCU-M gets disabled, undock from the target. The problem is that the
			// logic in (base) Attack activity breaks when trying to repair HCU-M that is in docked position.
			// Reason is that it cannot find nearest cell (due to range of the repair weapon) to the disabled HCU-M.
			// (see: MoveWithinRange.CalculatePathToTarget).
			// As a result it is not possible to repair the disabled HCU-M. By forcefully undocking HCU-M when it gets disabled,
			// it moves to the center of the cell, thus becomes accessible to other HCU-M.

			// TODO: fix broken attack logic when trying to repair disabled HCU-M that is in docked position.
			this.UndockFromTarget(self);

			return true;
		}

		if (this.IsCanceling)
		{
			if (this.RepairState is (RepairState.Repairing or RepairState.DockingToTarget))
				this.UndockFromTarget(self);

			return true;
		}

		this.target = this.target.Recalculate(self.Owner, out var targetIsHiddenActor);

		if (!targetIsHiddenActor && this.target.Type == TargetType.Actor)
		{
			this.lastVisibleTarget = Target.FromTargetPositions(this.target);
			this.lastVisibleMinRange = this.attack.GetMinimumRangeVersusTarget(this.target);
			this.lastVisibleMaxRange = this.attack.GetMaximumRangeVersusTarget(this.target);
			this.lastVisibleOwner = this.target.Actor.Owner;
			this.lastVisibleTargetTypes = this.target.Actor.GetEnabledTargetTypes();
		}

		this.useLastVisibleTarget = targetIsHiddenActor || !this.target.IsValidFor(self);

		var pos = self.CenterPosition;
		var checkTarget = this.useLastVisibleTarget ? this.lastVisibleTarget : this.target;

		if (checkTarget.Actor == null || checkTarget.Actor.IsInWorld == false)
		{
			if (this.RepairState is (RepairState.Repairing or RepairState.DockingToTarget))
				this.UndockFromTarget(self);

			return true;
		}

		switch (this.RepairState)
		{
			case RepairState.None or RepairState.MovingToTarget:
			{
				// Range check: if too far, move close to the target
				if (!checkTarget.IsInRange(pos, this.lastVisibleMaxRange) || checkTarget.IsInRange(pos, this.lastVisibleMinRange))
				{
					if (!this.allowMovement || this.lastVisibleMaxRange == WDist.Zero || this.lastVisibleMaxRange < this.lastVisibleMinRange)
						return true;

					this.QueueChild(
						this.mobile.MoveWithinRange(this.target, this.lastVisibleMinRange, this.lastVisibleMaxRange, checkTarget.CenterPosition, Color.Red)
					);

					return false;
				}

				var targetMobile = this.target.Actor.TraitOrDefault<Mobile>();
				var targetSubCell = targetMobile != null ? targetMobile.ToSubCell : SubCell.Any;

				// Facing check: if not facing target, turn
				var destination = self.World.Map.CenterOfSubCell(this.target.Actor.Location, targetSubCell);
				var origin = self.World.Map.CenterOfSubCell(self.Location, this.mobile.FromSubCell);
				var desiredFacing = (destination - origin).Yaw;

				if (this.mobile.Facing != desiredFacing)
				{
					this.QueueChild(new Turn(self, desiredFacing));

					return false;
				}

				// If target is moving, wait a bit, before attempting to dock again.
				if (targetMobile?.CurrentMovementTypes != MovementType.None)
				{
					this.QueueChild(new Wait(5));

					return false;
				}

				// Ready to dock to target
				this.RepairState = RepairState.DockingToTarget;

				foreach (var n in this.notifyRepair)
					n.Docking(self);

				return false;
			}

			case RepairState.DockingToTarget:
			{
				// Re-check if docking is still possible
				if (this.target.Type == TargetType.Invalid || !this.CanDock(self, this.target.Actor))
				{
					this.RepairState = RepairState.MovingToTarget;

					return false;
				}

				// Calculate repair position
				var vec = this.target.Actor.Location - self.Location;
				var repairPosition = pos + new WVec(vec.X * 256, vec.Y * 256, 0);
				var length = (pos - repairPosition).Length / this.GetDockSpeed();

				// do first move tick manually (to fake appearance of continuous movement)
				var nextPos = WPos.Lerp(pos, repairPosition, 0, length - 1);

				this.mobile.SetCenterPosition(self, nextPos);

				this.QueueChild(new Drag(self, pos, repairPosition, length));
				this.RepairState = RepairState.DockingToTarget;

				//this.attack.DockingToTarget(self);

				this.QueueChild(new CallFunc(() => this.RepairState = RepairState.Repairing, false));

				break;
			}

			case RepairState.Repairing:
			{
				// Re-check if the target is still valid
				if (this.target.Type == TargetType.Invalid)
				{
					this.UndockFromTarget(self);

					return true;
				}

				// Check if target isn't moving, if so, undock and try moving to it again
				var targetMobile = this.target.Actor.TraitOrDefault<Mobile>();

				if (targetMobile.CurrentMovementTypes != MovementType.None)
				{
					this.UndockFromTarget(self);
					this.RepairState = RepairState.MovingToTarget;

					return false;
				}

				// Check if the repair is complete, if so, undock from target
				if (this.target.Actor.Trait<Health>().DamageState == DamageState.Undamaged)
				{
					this.UndockFromTarget(self);

					return true;
				}

				foreach (var n in this.notifyRepair)
					n.Repairing(self);

				// Repair target
				this.attack.DoAttack(self, this.target);

				break;
			}

			case RepairState.UndockingFromTarget:
			{
				// Is this necessary?
				break;
			}
		}

		return false;
	}

	private void UndockFromTarget(Actor self)
	{
		var returnPosition = self.World.Map.CenterOfCell(self.Location);
		var length = (self.CenterPosition - returnPosition).Length / this.GetUndockSpeed(self);

		this.QueueChild(new Drag(self, self.CenterPosition, returnPosition, length));

		this.QueueChild(
			new CallFunc(
				() =>
				{
					foreach (var n in this.notifyRepair)
						n.Undocked(self);
				},
				false
			)
		);

		this.RepairState = RepairState.UndockingFromTarget;

		foreach (var n in this.notifyRepair)
			n.Undocking(self);
	}

	private int GetDockSpeed()
	{
		return this.mobile.Locomotor.MovementSpeedForCell(this.target.Actor.Location) * this.attack.Info.DockSpeedModifier / 100;
	}

	private int GetUndockSpeed(Actor self)
	{
		return this.mobile.Locomotor.MovementSpeedForCell(self.Location) * this.attack.Info.DockSpeedModifier / 100;
	}

	private bool CanDock(Actor self, Actor target)
	{
		var targetMobile = target.TraitOrDefault<Mobile>();

		if (targetMobile.CurrentMovementTypes != MovementType.None)
			return false;

		var cellDist = this.target.Actor.Location - self.Location;

		if (!RepairAttack.AllowedDockDirections.Contains(cellDist))
			return false;

		if (self.CenterPosition != self.World.Map.CenterOfCell(self.Location) || target.CenterPosition != self.World.Map.CenterOfCell(target.Location))
			return false;

		return true;
	}

	protected override void OnLastRun(Actor self)
	{
		this.attack.IsAiming = false;
	}

	void IActivityNotifyStanceChanged.StanceChanged(Actor self, AutoTarget autoTarget, UnitStance oldStance, UnitStance newStance)
	{
		// Cancel non-forced targets when switching to a more restrictive stance if they are no longer valid for auto-targeting
		if (newStance > oldStance || this.forceAttack)
			return;

		// If lastVisibleTarget is invalid we could never view the target in the first place, so we just drop it here too
		if (!this.lastVisibleTarget.IsValidFor(self) || !autoTarget.HasValidTargetPriority(self, this.lastVisibleOwner, this.lastVisibleTargetTypes))
			this.target = Target.Invalid;
	}

	public override IEnumerable<TargetLineNode> TargetLineNodes(Actor self)
	{
		if (this.targetLineColor != null)
			yield return new TargetLineNode(this.useLastVisibleTarget ? this.lastVisibleTarget : this.target, this.targetLineColor.Value);
	}
}
