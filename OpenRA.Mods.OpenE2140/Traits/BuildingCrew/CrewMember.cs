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

using OpenRA;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Activites;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Primitives;
using OpenRA.Support;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.BuildingCrew;

[Desc($"This actor can occupy other {nameof(BuildingCrew)} actors.")]
public class CrewMemberInfo : TraitInfo, IObservesVariablesInfo
{
	[GrantedConditionReference]
	[Desc("The condition to grant to when this actor entered a building.")]
	public readonly string? BuildingCrewCondition;

	[ActorReference(dictionaryReference: LintDictionaryReference.Keys)]
	[Desc("Conditions to grant when this actor entered inside of a building.",
		"A dictionary of [actor name]: [condition].")]
	public readonly Dictionary<string, string> BuildingCrewConditions = [];

	[GrantedConditionReference]
	public IEnumerable<string> LinterBuildingCrewConditions => this.BuildingCrewConditions.Values;

	[VoiceReference]
	public readonly string Voice = "Action";

	[Desc("Color to use for the target line.")]
	public readonly Color TargetLineColor = Color.Green;

	[ConsumedConditionReference]
	[Desc("Boolean expression defining the condition under which the regular (non-force) enter cursor is disabled.")]
	public readonly BooleanExpression? RequireForceMoveCondition;

	[CursorReference]
	[Desc("Cursor to display when able to enter target actor.")]
	public readonly string EnterCursor = "enter";

	[CursorReference]
	[Desc("Cursor to display when unable to enter target actor.")]
	public readonly string EnterBlockedCursor = "enter-blocked";

	[Desc("When exiting building, cancel all other activies.")]
	public readonly bool CancelActivitiesOnExit;

	public override object Create(ActorInitializer init) { return new CrewMember(init.Self, this); }
}

public class CrewMember : IIssueOrder, IResolveOrder, IOrderVoice, INotifyRemovedFromWorld, INotifyEnteredBuildingCrew, INotifyExitedBuildingCrew, IObservesVariables
{
	private const string EnterBuildingOrderID = "EnterBuilding";

	private readonly Actor actor;
	public readonly CrewMemberInfo Info;
	public Actor? BuildingCrew;
	private bool requireForceMove;
	private int anyCrewMemberToken = Actor.InvalidConditionToken;
	private int specificCrewMemberToken = Actor.InvalidConditionToken;

	public BuildingCrew? ReservedCrew { get; private set; }

	public CrewMember(Actor actor, CrewMemberInfo info)
	{
		this.actor = actor;
		this.Info = info;
	}

	IEnumerable<IOrderTargeter> IIssueOrder.Orders
	{
		get
		{
			yield return new EnterActorTargeter<BuildingCrewInfo>(
				order: EnterBuildingOrderID,
				priority: 5,
				enterCursor: this.Info.EnterCursor,
				enterBlockedCursor: this.Info.EnterBlockedCursor,
				canTarget: this.CanTarget,
				canTargetFrozen: this.CanTargetFrozen,
				useEnterCursor: this.CanEnter);
		}
	}

	public Order? IssueOrder(Actor self, IOrderTargeter order, in Target target, bool queued)
	{
		if (order.OrderID == EnterBuildingOrderID)
			return new Order(order.OrderID, self, target, queued);

		return null;
	}

	private bool CanTarget(Actor target, TargetModifiers modifiers)
	{
		return !this.requireForceMove || modifiers.HasModifier(TargetModifiers.ForceMove) || target.Owner.RelationshipWith(this.actor.Owner) != PlayerRelationship.Ally;
	}

	private bool CanTargetFrozen(FrozenActor target, TargetModifiers modifiers)
	{
		return !this.requireForceMove || modifiers.HasModifier(TargetModifiers.ForceMove) || target.Owner.RelationshipWith(this.actor.Owner) != PlayerRelationship.Ally;
	}

	private bool CanEnter(Actor target)
	{
		return target != null && target.TryGetTrait<BuildingCrew>(out var crew) && !crew.IsTraitDisabled && crew.CanEnter(this.actor);
	}

	public string? VoicePhraseForOrder(Actor self, Order order)
	{
		if (order.OrderString != EnterBuildingOrderID)
			return null;

		if (order.Target.Type != TargetType.Actor || !this.CanEnter(order.Target.Actor))
			return null;

		return this.Info.Voice;
	}

	void INotifyEnteredBuildingCrew.OnEnteredBuildingCrew(Actor self, Actor buildingCrew)
	{
		if (this.anyCrewMemberToken == Actor.InvalidConditionToken)
			this.anyCrewMemberToken = self.GrantCondition(this.Info.BuildingCrewCondition);

		if (this.specificCrewMemberToken == Actor.InvalidConditionToken && this.Info.BuildingCrewConditions.TryGetValue(buildingCrew.Info.Name, out var specificCrewMemberCondition))
			this.specificCrewMemberToken = self.GrantCondition(specificCrewMemberCondition);

		// Allow scripted / initial actors to move from the unload point back into the cell grid on unload
		// This is handled by the EnterCrewMember activity for player-entered building crew
		if (self.IsIdle)
		{
			// IMove is not used anywhere else in this trait, there is no benefit to caching it from Created.
			var move = self.TraitOrDefault<IMove>();
			if (move != null)
				self.QueueActivity(move.ReturnToCell(self));
		}
	}

	void INotifyExitedBuildingCrew.OnExitedBuildingCrew(Actor self, Actor buildingCrew)
	{
		if (this.anyCrewMemberToken != Actor.InvalidConditionToken)
			this.anyCrewMemberToken = self.RevokeCondition(this.anyCrewMemberToken);

		if (this.specificCrewMemberToken != Actor.InvalidConditionToken)
			this.specificCrewMemberToken = self.RevokeCondition(this.specificCrewMemberToken);
	}

	void IResolveOrder.ResolveOrder(Actor self, Order order)
	{
		if (order.OrderString != EnterBuildingOrderID)
			return;

		// Enter orders are only valid for own/allied actors,
		// which are guaranteed to never be frozen.
		if (order.Target.Type != TargetType.Actor)
			return;

		var targetActor = order.Target.Actor;
		if (!this.CanEnter(targetActor))
			return;

		self.QueueActivity(order.Queued, new EnterCrewMember(self, order.Target, this.Info.TargetLineColor));
		self.ShowTargetLines();
	}

	public bool Reserve(Actor self, BuildingCrew crew)
	{
		if (crew == this.ReservedCrew)
			return true;

		this.Unreserve(self);
		if (!crew.ReserveSpace(self))
			return false;

		this.ReservedCrew = crew;
		return true;
	}

	void INotifyRemovedFromWorld.RemovedFromWorld(Actor self) { this.Unreserve(self); }

	public void Unreserve(Actor self)
	{
		if (this.ReservedCrew == null)
			return;

		this.ReservedCrew.UnreserveSpace(self);
		this.ReservedCrew = null;
	}

	IEnumerable<VariableObserver> IObservesVariables.GetVariableObservers()
	{
		if (this.Info.RequireForceMoveCondition != null)
			yield return new VariableObserver(this.RequireForceMoveConditionChanged, this.Info.RequireForceMoveCondition.Variables);
	}

	private void RequireForceMoveConditionChanged(Actor self, IReadOnlyDictionary<string, int> conditions)
	{
		this.requireForceMove = this.Info.RequireForceMoveCondition?.Evaluate(conditions) ?? false;
	}
}
