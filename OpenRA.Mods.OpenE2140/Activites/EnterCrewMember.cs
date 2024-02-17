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
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Traits.BuildingCrew;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Activites;

public class EnterCrewMember : Activity
{
	private enum EnterState { Approaching, Entering, Exiting, Finished }

	private readonly CrewMember crewMember;
	private readonly IMove move;
	private readonly Color? targetLineColor;
	private Target target;
	private readonly BuildingCrew buildingCrew;
	private Target lastVisibleTarget;
	private bool useLastVisibleTarget;
	private EnterState lastState = EnterState.Approaching;
	private Actor? enterActor;

	public EnterCrewMember(Actor self, in Target target, Color? targetLineColor)
	{
		this.crewMember = self.Trait<CrewMember>();
		this.move = self.Trait<IMove>();
		this.target = target;
		this.buildingCrew = target.Actor.Trait<BuildingCrew>();
		this.targetLineColor = targetLineColor;
		this.ChildHasPriority = false;
	}

	public override bool Tick(Actor self)
	{
		// Update our view of the target
		this.target = this.target.Recalculate(self.Owner, out var targetIsHiddenActor);

		// When current target gets invalidated, check if we can re-target existing actor (but only if we're entering or approaching!)
		if (this.target.Type == TargetType.Invalid && this.lastState is EnterState.Entering or EnterState.Approaching)
		{
			if (this.target.FrozenActor != null)
				this.target = Target.FromFrozenActor(this.target.FrozenActor);
			else if (this.target.Actor != null)
				this.target = Target.FromActor(this.target.Actor);

			if (this.target.Type != TargetType.Invalid)
			{
				this.target = this.target.Recalculate(self.Owner, out targetIsHiddenActor);
				this.lastState = EnterState.Approaching;
			}
		}

		if (!targetIsHiddenActor && this.target.Type == TargetType.Actor)
			this.lastVisibleTarget = Target.FromTargetPositions(this.target);

		this.useLastVisibleTarget = targetIsHiddenActor || !this.target.IsValidFor(self);

		// Cancel immediately if the target died while we were entering it
		if (!this.IsCanceling && this.useLastVisibleTarget && this.lastState == EnterState.Entering)
			this.Cancel(self, true);

		this.TickInner(self, this.target, this.useLastVisibleTarget);

		// We need to wait for movement to finish before transitioning to
		// the next state or next activity
		if (!this.TickChild(self))
			return false;

		// Note that lastState refers to what we have just *finished* doing
		switch (this.lastState)
		{
			case EnterState.Approaching:
			{
				// NOTE: We can safely cancel in this case because we know the
				// actor has finished any in-progress move activities
				if (this.IsCanceling)
					return true;

				// Lost track of the target
				if (this.useLastVisibleTarget && this.lastVisibleTarget.Type == TargetType.Invalid)
					return true;

				// We are not next to the target - lets fix that
				if (this.target.Type != TargetType.Invalid && !this.CanEnterTargetNow(self, this.target))
				{
					// Target lines are managed by this trait, so we do not pass targetLineColor
					var initialTargetPosition = (this.useLastVisibleTarget ? this.lastVisibleTarget : this.target).CenterPosition;
					this.QueueChild(new MoveToBuildingEntrance(self, this.target, initialTargetPosition));
					return false;
				}

				// We are next to where we thought the target should be, but it isn't here
				// There's not much more we can do here
				if (this.useLastVisibleTarget || this.target.Type != TargetType.Actor || this.target.Actor == null)
					return true;

				// Are we ready to move into the target?
				if (this.TryStartEnter(self, this.target.Actor))
				{
					this.lastState = EnterState.Entering;

					//var nearestFootprintCell = this.BuildingOccupiedFootprintCells.MinBy(cell => (self.World.Map.CenterOfCell(cell) - self.CenterPosition).HorizontalLengthSquared);
					var nearestFootprintCell = this.buildingCrew.NearesetFootprintCell(self.CenterPosition);

					this.QueueChild(this.move.MoveIntoTarget(self, Target.FromCell(self.World, nearestFootprintCell)));
					return false;
				}

				// Subclasses can cancel the activity during TryStartEnter
				// Return immediately to avoid an extra tick's delay
				if (this.IsCanceling)
					return true;

				return false;
			}

			case EnterState.Entering:
			{
				// Check that we reached the requested position
				var targetPositions = this.buildingCrew.BuildingOccupiedFootprintCells.Select(c => self.World.Map.CenterOfCell(c));
				if (!this.IsCanceling && targetPositions.Contains(self.CenterPosition) && this.target.Type == TargetType.Actor)
					this.OnEnterComplete(self, this.target.Actor!);

				this.lastState = EnterState.Exiting;
				return false;
			}

			case EnterState.Exiting:
			{
				// It's possible that another EnterCrewMember activity has been queued with the same target building.
				// If so, exit immediately, the next EnterCrewMember activity will pick up where the current EnterCrewMember activity has left off.
				if (this.IsCanceling && this.NextActivity is EnterCrewMember nextEnter && nextEnter.target.Actor == this.enterActor)
				{
					return true;
				}

				this.QueueChild(this.move.ReturnToCell(self));
				this.lastState = EnterState.Finished;
				return false;
			}
		}

		return true;
	}

	private bool CanEnterTargetNow(Actor self, Target target)
	{
		if (target.Type == TargetType.FrozenActor && !target.FrozenActor.IsValid)
			return false;

		return self.Location == self.World.Map.CellContaining(target.CenterPosition) || this.buildingCrew.EntryCells.Any(c => c == self.Location);
	}

	protected virtual void TickInner(Actor self, in Target target, bool targetIsDeadOrHiddenActor)
	{
		if (this.buildingCrew.IsTraitDisabled)
			this.Cancel(self, true);
	}

	protected virtual bool TryStartEnter(Actor self, Actor targetActor)
	{
		this.enterActor = targetActor;

		// Make sure we can still enter the building
		// (but not before, because this may stop the actor in the middle of nowhere)
		if (this.buildingCrew.IsTraitDisabled || !this.crewMember.Reserve(self, this.buildingCrew))
		{
			this.Cancel(self, true);
			return false;
		}

		return true;
	}

	protected virtual void OnEnterComplete(Actor self, Actor targetActor)
	{
		self.World.AddFrameEndTask(w =>
		{
			if (self.IsDead)
				return;

			// Make sure the target hasn't changed while entering
			// OnEnterComplete is only called if targetActor is alive
			if (targetActor != this.enterActor)
				return;

			if (!this.buildingCrew.CanEnter(self))
				return;

			foreach (var iecm in targetActor.TraitsImplementing<INotifyEnterCrewMember>())
				iecm.Entering(self);

			this.buildingCrew.Enter(this.enterActor, self);
			w.Remove(self);
		});
	}

	protected override void OnLastRun(Actor self)
	{
		this.crewMember.Unreserve(self);
	}

	public override void Cancel(Actor self, bool keepQueue = false)
	{
		this.crewMember.Unreserve(self);

		base.Cancel(self, keepQueue);
	}

	public override IEnumerable<TargetLineNode> TargetLineNodes(Actor self)
	{
		if (this.targetLineColor != null)
			yield return new TargetLineNode(this.useLastVisibleTarget ? this.lastVisibleTarget : this.target, this.targetLineColor.Value);
	}
}
