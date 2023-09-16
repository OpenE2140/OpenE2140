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
using OpenRA.Mods.Common.Orders;
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
	public readonly string? CargoType = null;

	[Desc("If defined, use a custom pip type defined on the transport's WithCargoPipsDecoration.CustomPipSequences list.")]
	public readonly string? CustomPipType = null;

	[GrantedConditionReference]
	[Desc("The condition to grant to when this actor is loaded inside any transport.")]
	public readonly string? CargoCondition = null;

	[ActorReference(dictionaryReference: LintDictionaryReference.Keys)]
	[Desc("Conditions to grant when this actor is loaded inside specified transport.",
		"A dictionary of [actor name]: [condition].")]
	public readonly Dictionary<string, string> CargoConditions = new();

	[GrantedConditionReference]
	public IEnumerable<string> LinterCargoConditions => this.CargoConditions.Values;

	[VoiceReference]
	public readonly string Voice = "Action";

	[Desc("Color to use for the target line.")]
	public readonly Color TargetLineColor = Color.Green;

	[ConsumedConditionReference]
	[Desc("Boolean expression defining the condition under which the regular (non-force) enter cursor is disabled.")]
	public readonly BooleanExpression? RequireForceMoveCondition = null;

	[CursorReference]
	[Desc("Cursor to display when able to enter target actor.")]
	public readonly string EnterCursor = "enter";

	[CursorReference]
	[Desc("Cursor to display when unable to enter target actor.")]
	public readonly string EnterBlockedCursor = "enter-blocked";

	public override object Create(ActorInitializer init) { return new CrewMember(this); }
}

public class CrewMember : IIssueOrder, IResolveOrder, IOrderVoice, INotifyRemovedFromWorld, INotifyEnteredCargo, INotifyExitedCargo, INotifyKilled, IObservesVariables
{
	private const string OrderID = "EnterCrew";
	public readonly CrewMemberInfo Info;
	public Actor BuildingCrew;
	private bool requireForceMove;
	private int anyCargoToken = Actor.InvalidConditionToken;
	private int specificCargoToken = Actor.InvalidConditionToken;

	public CrewMember(CrewMemberInfo info)
	{
		this.Info = info;
	}

	public BuildingCrew ReservedCrew { get; private set; }

	IEnumerable<IOrderTargeter> IIssueOrder.Orders
	{
		get
		{
			yield return new EnterAlliedActorTargeter<BuildingCrewInfo>(
				OrderID,
				5,
				this.Info.EnterCursor,
				this.Info.EnterBlockedCursor,
				this.CanTarget,
				this.CanEnter);
		}
	}

	public Order IssueOrder(Actor self, IOrderTargeter order, in Target target, bool queued)
	{
		if (order.OrderID == OrderID)
			return new Order(order.OrderID, self, target, queued);

		return null;
	}

	private bool CanTarget(Actor target, TargetModifiers modifiers)
	{
		return !this.requireForceMove || modifiers.HasModifier(TargetModifiers.ForceMove);
	}

	private bool CanEnter(Actor target)
	{
		return target.TryGetTrait<BuildingCrew>(out var crew) && !crew.IsTraitDisabled && crew.HasSpace();
	}

	public string VoicePhraseForOrder(Actor self, Order order)
	{
		if (order.OrderString != OrderID)
			return null;

		if (order.Target.Type != TargetType.Actor || !this.CanEnter(order.Target.Actor))
			return null;

		return this.Info.Voice;
	}

	void INotifyEnteredCargo.OnEnteredCargo(Actor self, Actor cargo)
	{
		if (this.anyCargoToken == Actor.InvalidConditionToken)
			this.anyCargoToken = self.GrantCondition(this.Info.CargoCondition);

		if (this.specificCargoToken == Actor.InvalidConditionToken && this.Info.CargoConditions.TryGetValue(cargo.Info.Name, out var specificCargoCondition))
			this.specificCargoToken = self.GrantCondition(specificCargoCondition);

		// Allow scripted / initial actors to move from the unload point back into the cell grid on unload
		// This is handled by the RideTransport activity for player-loaded cargo
		if (self.IsIdle)
		{
			// IMove is not used anywhere else in this trait, there is no benefit to caching it from Created.
			var move = self.TraitOrDefault<IMove>();
			if (move != null)
				self.QueueActivity(move.ReturnToCell(self));
		}
	}

	void INotifyExitedCargo.OnExitedCargo(Actor self, Actor cargo)
	{
		if (this.anyCargoToken != Actor.InvalidConditionToken)
			this.anyCargoToken = self.RevokeCondition(this.anyCargoToken);

		if (this.specificCargoToken != Actor.InvalidConditionToken)
			this.specificCargoToken = self.RevokeCondition(this.specificCargoToken);
	}

	void IResolveOrder.ResolveOrder(Actor self, Order order)
	{
		if (order.OrderString != OrderID)
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

	void INotifyKilled.Killed(Actor self, AttackInfo e)
	{
		if (this.BuildingCrew == null)
			return;

		// Something killed us, but it wasn't our transport blowing up. Remove us from the cargo.
		if (!this.BuildingCrew.IsDead)
			this.BuildingCrew.Trait<BuildingCrew>().Unload(this.BuildingCrew, self);
	}

	IEnumerable<VariableObserver> IObservesVariables.GetVariableObservers()
	{
		if (this.Info.RequireForceMoveCondition != null)
			yield return new VariableObserver(this.RequireForceMoveConditionChanged, this.Info.RequireForceMoveCondition.Variables);
	}

	private void RequireForceMoveConditionChanged(Actor self, IReadOnlyDictionary<string, int> conditions)
	{
		this.requireForceMove = this.Info.RequireForceMoveCondition.Evaluate(conditions);
	}
}
