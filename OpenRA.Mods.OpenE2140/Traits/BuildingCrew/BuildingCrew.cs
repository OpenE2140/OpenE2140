﻿#region Copyright & License Information

/*
 * Copyright (c) The OpenE2140 Developers and Contributors
 * This file is part of OpenE2140, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */

#endregion

using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Activites;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Mods.OpenE2140.Traits.Buildings;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.BuildingCrew;

[Desc("This actor has crew and can be conquered.")]
public class BuildingCrewInfo : ConditionalTraitInfo, Requires<BuildingInfo>, Requires<BuildingCrewEntranceInfo>
{
	[Desc("The maximum number of crew members this actor can have.")]
	public readonly int MaxPopulation = 0;

	[Desc("A list of actor types that are initially spawned into this actor.")]
	public readonly string[] InitialUnits = [];

	[Desc("A list of 0 or more offsets for cells which serve as points of entry into the building (i.e. to one of the footprint cells)." +
		"If none is defined, the future crew member can enter the building from any cell around the building.")]
	public readonly CVec[] EntryCells = [];

	[Desc("Are crew members allowed to exit the building?")]
	public readonly bool AllowCrewMemberExit = true;

	[Desc("When this actor is sold, should all of its crew members exit the building?")]
	public readonly bool EjectOnSell = true;

	[Desc("When this actor dies, should all of its crew members exit the building?")]
	public readonly bool EjectOnDeath = false;

	[Desc("Terrain types that this actor is allowed to eject actors onto. Leave empty for all terrain types.")]
	public readonly HashSet<string> ExitTerrainTypes = [];

	[NotificationReference("Speech")]
	[Desc("Speech notification to play when a crew member exits the building.")]
	public readonly string? ExitBuildingNotification;

	[Desc("Which direction the crew members will face (relative to the transport) when exiting.")]
	public readonly WAngle CrewMemberFacing = new(512);

	[Desc("Delay (in ticks) before the first crew member exits.")]
	public readonly int BeforeExitDelay = 0;

	[Desc("Delay (in ticks) before continuing after a crew member exits.")]
	public readonly int AfterExitDelay = 25;

	[CursorReference]
	[Desc("Cursor to display when able to make the crew member exit the building.")]
	public readonly string CrewExitCursor = "deploy";

	[CursorReference]
	[Desc("Cursor to display when unable to make the crew member exit building.")]
	public readonly string CrewExitBlockedCursor = "deploy-blocked";

	[GrantedConditionReference]
	[Desc("The condition to grant to self while waiting for crew member to enter.")]
	public readonly string? EnteringCondition = null;

	[GrantedConditionReference]
	[Desc("The condition to grant to self while crew members are entering.",
		"Condition can stack with multiple crew members.")]
	public readonly string? EnteredCondition = null;

	[ActorReference(dictionaryReference: LintDictionaryReference.Keys)]
	[Desc("Conditions to grant when specified actors have entered inside the building.",
		"A dictionary of [actor name]: [condition].")]
	public readonly Dictionary<string, string> CrewMemberConditions = [];

	[GrantedConditionReference]
	public IEnumerable<string> LinterCrewMemberConditions => this.CrewMemberConditions.Values;

	[Desc("Default value for offset towards which the entering actor is moving to enter the building. Relative to the center of the building actor.",
		"If null, center of the nearest footprint cell is chosen.")]
	public readonly WVec? DefaultEntranceOffset;

	public override object Create(ActorInitializer init) { return new BuildingCrew(init, this); }
}

public class BuildingCrew : ConditionalTrait<BuildingCrewInfo>, IIssueOrder, IResolveOrder,
	INotifyOwnerChanged, INotifySold, INotifyActorDisposing, IIssueDeployOrder,
	INotifyCreated, INotifyKilled, INotifyDamage, INotifySelfDestruction
{
	private const string ExitBuildingOrderID = "ExitBuilding";

	private readonly Actor self;
	private readonly List<Actor> crewMembers = [];
	private readonly HashSet<Actor> reservations = [];
	private readonly HashSet<Actor> conquerReservations = [];
	private readonly Dictionary<string, Stack<int>> crewMemberTokens = [];
	private readonly Lazy<IFacing> facing;
	private readonly bool checkTerrainType;
	private int enteringToken = Actor.InvalidConditionToken;
	private readonly Stack<int> enteredTokens = new();
	private bool initialised;

	private Player? conqueredByPlayer;

	private Player EffectiveOwner => this.conqueredByPlayer ?? this.self.Owner;

	public IEnumerable<CPos> BuildingOccupiedFootprintCells { get; }

	public IReadOnlyList<BuildingCrewEntrance> Entrances { get; private set; } = Array.Empty<BuildingCrewEntrance>();

	public IReadOnlyCollection<Actor> CrewMembers => this.crewMembers;
	public int MemberCount => this.crewMembers.Count;

	public BuildingCrew(ActorInitializer init, BuildingCrewInfo info)
		: base(info)
	{
		this.self = init.Self;
		this.checkTerrainType = info.ExitTerrainTypes.Count > 0;
		this.Entrances = this.self.TraitsImplementing<BuildingCrewEntrance>().ToArray();
		this.BuildingOccupiedFootprintCells = this.self.Info.TraitInfo<BuildingInfo>().FootprintTiles(this.self.Location, FootprintCellType.Occupied);

		var buildingCrewInit = init.GetOrDefault<BuildingCrewInit>(info);
		if (buildingCrewInit != null)
		{
			foreach (var u in buildingCrewInit.Value)
			{
				var unit = this.self.World.CreateActor(false, u.ToLowerInvariant(),
					[new OwnerInit(this.self.Owner)]);

				this.crewMembers.Add(unit);
			}
		}
		else
		{
			foreach (var u in info.InitialUnits)
			{
				var unit = this.self.World.CreateActor(false, u.ToLowerInvariant(),
					[new OwnerInit(this.self.Owner)]);

				this.crewMembers.Add(unit);
			}
		}

		this.facing = Exts.Lazy(this.self.TraitOrDefault<IFacing>);
	}

	protected override void Created(Actor self)
	{
		base.Created(self);

		if (this.crewMembers.Count > 0)
		{
			foreach (var c in this.crewMembers)
				if (this.Info.CrewMemberConditions.TryGetValue(c.Info.Name, out var crewMemberCondition))
					this.crewMemberTokens.GetOrAdd(c.Info.Name).Push(self.GrantCondition(crewMemberCondition));

			if (!string.IsNullOrEmpty(this.Info.EnteredCondition))
				this.enteredTokens.Push(self.GrantCondition(this.Info.EnteredCondition));
		}

		// Defer notifications until we are certain all traits on the transport are initialised
		self.World.AddFrameEndTask(w =>
		{
			foreach (var crewMember in this.crewMembers)
			{
				crewMember.Trait<CrewMember>().BuildingCrew = self;

				foreach (var nebc in crewMember.TraitsImplementing<INotifyEnteredBuildingCrew>())
					nebc.OnEnteredBuildingCrew(crewMember, self);

				foreach (var ncme in self.TraitsImplementing<INotifyCrewMemberEntered>())
					ncme.OnCrewMemberEntered(self, crewMember);
			}

			this.initialised = true;
		});
	}

	public IEnumerable<IOrderTargeter> Orders
	{
		get
		{
			if (this.IsTraitDisabled)
				yield break;

			yield return new DeployOrderTargeter(ExitBuildingOrderID, 10,
				() => this.CanExit() ? this.Info.CrewExitCursor : this.Info.CrewExitBlockedCursor);
		}
	}

	public Order? IssueOrder(Actor self, IOrderTargeter order, in Target target, bool queued)
	{
		if (order.OrderID == ExitBuildingOrderID)
			return new Order(order.OrderID, self, queued);

		return null;
	}

	Order IIssueDeployOrder.IssueDeployOrder(Actor self, bool queued)
	{
		return new Order(ExitBuildingOrderID, self, queued);
	}

	bool IIssueDeployOrder.CanIssueDeployOrder(Actor self, bool queued) { return true; }

	public void ResolveOrder(Actor self, Order order)
	{
		if (order.OrderString == ExitBuildingOrderID)
		{
			if (!this.CanExit())
				return;

			self.QueueActivity(order.Queued, new CrewExit(self));
		}
	}

	public bool CanExit(BlockedByActor check = BlockedByActor.None)
	{
		if (this.IsTraitDisabled || !this.Info.AllowCrewMemberExit)
			return false;

		if (this.checkTerrainType)
		{
			var terrainType = this.self.World.Map.GetTerrainInfo(this.self.Location).Type;

			if (!this.Info.ExitTerrainTypes.Contains(terrainType))
				return false;
		}

		return !this.IsEmpty() && this.Entrances.Any(e => this.CrewMembers.Any(p => !p.IsDead && p.Trait<IPositionable>().CanEnterCell(e.EntryCell, null, check)));
	}

	public (BuildingCrewEntrance entrance, SubCell subCell)? RandomCrewEntranceOrDefault(Actor crewMember)
	{
		if (crewMember.Disposed)
			return null;

		var pos = crewMember.Trait<IPositionable>();

		foreach (var g in this.Entrances.GroupBy(e => e.Info.Priority))
		{
			var shuffled = g.Shuffle(this.self.World.SharedRandom);

			var valid = shuffled
				.Select(e => (entrance: e, subCell: pos.GetAvailableSubCell(e.EntryCell))).ToArray()
				.FirstOrDefault(e => e.subCell is not SubCell.Invalid);
			if (valid.entrance != null)
				return valid;
		}

		return null;
	}

	public bool CanEnter(Actor actor)
	{
		if (this.IsTraitDisabled)
			return false;

		if (actor.Owner == this.EffectiveOwner)
			return this.reservations.Contains(actor) || this.HasSpace();

		// Cannot enter buildings of allied players
		if (actor.Owner.RelationshipWith(this.self.Owner) == PlayerRelationship.Ally)
			return false;

		return this.conquerReservations.Contains(actor) || this.CanConquer();
	}

	internal bool ReserveSpace(Actor a)
	{
		if (this.reservations.Contains(a))
			return true;

		if (this.conquerReservations.Contains(a))
			return true;

		if (a.Owner != this.EffectiveOwner)
		{
			// Actor is attempting to conquer this building
			if (!this.CanConquer())
				return false;

			this.conquerReservations.Add(a);
		}
		else
		{
			// Building's owner is sending actors into it
			if (!this.HasSpace())
				return false;
			this.reservations.Add(a);
		}

		if (this.enteringToken == Actor.InvalidConditionToken)
			this.enteringToken = this.self.GrantCondition(this.Info.EnteringCondition);

		return true;
	}

	internal void UnreserveSpace(Actor a)
	{
		if (this.self.IsDead)
			return;

		if (this.reservations.Remove(a) || this.conquerReservations.Remove(a))
		{
			if (this.enteringToken != Actor.InvalidConditionToken)
				this.enteringToken = this.self.RevokeCondition(this.enteringToken);
		}
	}

	public bool HasSpace() { return this.crewMembers.Count + this.reservations.Count + 1 <= this.Info.MaxPopulation; }
	public bool CanConquer() { return this.conquerReservations.Count + 1 <= this.Info.MaxPopulation; }
	public bool IsEmpty() { return this.crewMembers.Count == 0; }

	public Actor Peek() { return this.crewMembers[^1]; }

	public Actor Exit(Actor self, Actor? crewMember = null)
	{
		crewMember ??= this.crewMembers[^1];
		if (!this.crewMembers.Remove(crewMember))
			throw new ArgumentException("Attempted to make an actor exit that is not a crew member.");

		this.SetCrewMemberFacing(crewMember);

		foreach (var npe in self.TraitsImplementing<INotifyCrewMemberExited>())
			npe.OnCrewMemberExited(self, crewMember);

		foreach (var nec in crewMember.TraitsImplementing<INotifyExitedBuildingCrew>())
			nec.OnExitedBuildingCrew(crewMember, self);

		var c = crewMember.Trait<CrewMember>();
		c.BuildingCrew = null;

		if (this.crewMemberTokens.TryGetValue(crewMember.Info.Name, out var crewMemberToken) && crewMemberToken.Count > 0)
			self.RevokeCondition(crewMemberToken.Pop());

		if (this.enteredTokens.Count > 0)
			self.RevokeCondition(this.enteredTokens.Pop());

		return crewMember;
	}

	private void SetCrewMemberFacing(Actor crewMember)
	{
		if (this.facing.Value == null)
			return;

		var crewMemberFacing = crewMember.TraitOrDefault<IFacing>();
		if (crewMemberFacing != null)
			crewMemberFacing.Facing = this.facing.Value.Facing + this.Info.CrewMemberFacing;
	}

	public void Enter(Actor self, Actor crewMember)
	{
		if (crewMember.Owner != this.EffectiveOwner)
		{
			// conquer in progress
			this.UnreserveSpace(crewMember);

			if (this.crewMembers.Count > 0)
			{
				// dispose the first crew member inside the building
				var defender = this.crewMembers[0];
				this.crewMembers.RemoveAt(0);
				if (defender.TryGetTrait<CrewMember>(out var k))
					k.BuildingCrew = null;
				defender.Kill(crewMember);

				if (this.crewMemberTokens.TryGetValue(crewMember.Info.Name, out var crewMemberToken) && crewMemberToken.Count > 0)
					self.RevokeCondition(crewMemberToken.Pop());

				if (this.enteredTokens.Count > 0)
					self.RevokeCondition(this.enteredTokens.Pop());

				crewMember.Trait<CrewMember>().BuildingCrew = self;

				// Fake the attacker being actually in the building, by notifying other traits (mainly CrewMember, which grants its conditions).
				// TODO: should the conditions be also granted?
				foreach (var nebc in crewMember.TraitsImplementing<INotifyEnteredBuildingCrew>())
					nebc.OnEnteredBuildingCrew(crewMember, self);

				foreach (var ncme in self.TraitsImplementing<INotifyCrewMemberEntered>())
					ncme.OnCrewMemberEntered(self, crewMember);

				self.World.Remove(crewMember);
				self.World.AddFrameEndTask(_ =>
				{
					crewMember.Kill(defender);
				});

				return;
			}
			else
			{
				// the original crew is gone, the building has been conquered, change owner
				this.conqueredByPlayer = crewMember.Owner;
				var oldOwner = self.Owner;
				self.World.AddFrameEndTask(_ =>
				{
					// If another player has conquered this building in this same tick, don't change the building's owner.
					if (this.conqueredByPlayer != crewMember.Owner)
						return;

					this.conqueredByPlayer = null;
					self.ChangeOwnerSync(crewMember.Owner);

					foreach (var t in self.TraitsImplementing<INotifyBuildingConquered>())
						t.OnConquering(self, crewMember, oldOwner, self.Owner);
				});
			}
		}

		this.crewMembers.Add(crewMember);
		if (this.reservations.Contains(crewMember))
		{
			this.reservations.Remove(crewMember);

			if (this.enteringToken != Actor.InvalidConditionToken)
				this.enteringToken = self.RevokeCondition(this.enteringToken);
		}

		// Don't initialise (effectively twice) if this runs before the FrameEndTask from Created
		if (this.initialised)
		{
			crewMember.Trait<CrewMember>().BuildingCrew = self;

			foreach (var nebc in crewMember.TraitsImplementing<INotifyEnteredBuildingCrew>())
				nebc.OnEnteredBuildingCrew(crewMember, self);

			foreach (var ncme in self.TraitsImplementing<INotifyCrewMemberEntered>())
				ncme.OnCrewMemberEntered(self, crewMember);
		}

		if (this.Info.CrewMemberConditions.TryGetValue(crewMember.Info.Name, out var crewMemberCondition))
			this.crewMemberTokens.GetOrAdd(crewMember.Info.Name).Push(self.GrantCondition(crewMemberCondition));

		if (!string.IsNullOrEmpty(this.Info.EnteredCondition))
			this.enteredTokens.Push(self.GrantCondition(this.Info.EnteredCondition));
	}

	public CPos NearesetFootprintCell(WPos currentPosition)
	{
		return this.BuildingOccupiedFootprintCells.MinBy(cell => (this.self.World.Map.CenterOfCell(cell) - currentPosition).HorizontalLengthSquared);
	}

	void INotifyDamage.Damaged(Actor self, AttackInfo e)
	{
		if (!this.CanExit())
			return;

		if (e.Damage.Value > 0 && e.DamageState == DamageState.Critical)
		{
			// Building is about to be destroyed, start evacuating the crew
			if (self.CurrentActivity is not CrewExit)
				self.QueueActivity(true, new CrewExit(self, exitAll: false, playNotification: false));
		}
	}

	void INotifySelfDestruction.SelfDestructionStarted(Actor self)
	{
		if (!this.CanExit())
			return;

		if (self.CurrentActivity is not CrewExit)
			self.QueueActivity(true, new CrewExit(self, exitAll: true, playNotification: false));
	}

	void INotifySelfDestruction.SelfDestructionAborted(Actor self)
	{
		// currently noop
	}

	void INotifyKilled.Killed(Actor self, AttackInfo e)
	{
		if (this.Info.EjectOnDeath)
			while (!this.IsEmpty() && this.CanExit(BlockedByActor.All))
			{
				var crewMember = this.Exit(self);
				var cp = self.CenterPosition;
				var inAir = self.World.Map.DistanceAboveTerrain(cp).Length != 0;
				var positionable = crewMember.Trait<IPositionable>();
				positionable.SetPosition(crewMember, self.Location);

				if (!inAir && positionable.CanEnterCell(self.Location, self, BlockedByActor.None))
				{
					self.World.AddFrameEndTask(w => w.Add(crewMember));
					var nbms = crewMember.TraitsImplementing<INotifyBlockingMove>();
					foreach (var nbm in nbms)
						nbm.OnNotifyBlockingMove(crewMember, crewMember);
				}
				else
					crewMember.Kill(e.Attacker);
			}

		foreach (var c in this.crewMembers)
			c.Kill(e.Attacker);

		this.crewMembers.Clear();
	}

	void INotifyActorDisposing.Disposing(Actor self)
	{
		foreach (var c in this.crewMembers)
			c.Dispose();

		this.crewMembers.Clear();
	}

	void INotifySold.Selling(Actor self) { }
	void INotifySold.Sold(Actor self)
	{
		if (!this.Info.EjectOnSell || this.crewMembers == null)
			return;

		while (!this.IsEmpty())
			this.SpawnCrewMember(this.Exit(self));
	}

	private void SpawnCrewMember(Actor crewMember)
	{
		this.self.World.AddFrameEndTask(w =>
		{
			w.Add(crewMember);
			crewMember.Trait<IPositionable>().SetPosition(crewMember, this.self.Location);

			// TODO: this won't work well for >1 actor as they should move towards the next enterable (sub) cell instead
		});
	}

	void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
	{
		if (this.crewMembers == null)
			return;

		foreach (var p in this.CrewMembers)
			p.ChangeOwner(newOwner);
	}
}

public class BuildingCrewInit : ValueActorInit<string[]>
{
	public BuildingCrewInit(TraitInfo info, string[] value)
		: base(info, value) { }
}
