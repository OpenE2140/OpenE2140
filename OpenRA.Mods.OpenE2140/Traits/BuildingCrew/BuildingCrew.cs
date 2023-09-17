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
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.OpenE2140.Activites;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.BuildingCrew;

[Desc("This actor has crew and can be conquered.")]
public class BuildingCrewInfo : ConditionalTraitInfo, Requires<IOccupySpaceInfo>
{
	[Desc("The maximum number of crew members this actor can have.")]
	public readonly int MaxPopulation = 0;

	[Desc("A list of actor types that are initially spawned into this actor.")]
	public readonly string[] InitialUnits = Array.Empty<string>();

	[Desc("When this actor is sold should all of its crew members be unloaded?")]
	public readonly bool EjectOnSell = true;

	[Desc("When this actor dies should all of its crew members be unloaded?")]
	public readonly bool EjectOnDeath = false;

	[Desc("Terrain types that this actor is allowed to eject actors onto. Leave empty for all terrain types.")]
	public readonly HashSet<string> UnloadTerrainTypes = new();

	[VoiceReference]
	[Desc("Voice to play when ordered to unload the crew members.")]
	public readonly string UnloadVoice = "Action";

	[Desc("Radius to search for a load/unload location if the ordered cell is blocked.")]
	public readonly WDist LoadRange = WDist.FromCells(5);

	[Desc("Which direction the crew members will face (relative to the transport) when unloading.")]
	public readonly WAngle CrewMemberFacing = new(512);

	[Desc("Delay (in ticks) before continuing after loading a crew member.")]
	public readonly int AfterLoadDelay = 8;

	[Desc("Delay (in ticks) before unloading the first crew member.")]
	public readonly int BeforeUnloadDelay = 8;

	[Desc("Delay (in ticks) before continuing after unloading a crew member.")]
	public readonly int AfterUnloadDelay = 25;

	[CursorReference]
	[Desc("Cursor to display when able to unload the crew member.")]
	public readonly string UnloadCursor = "deploy";

	[CursorReference]
	[Desc("Cursor to display when unable to unload the crew member.")]
	public readonly string UnloadBlockedCursor = "deploy-blocked";

	[GrantedConditionReference]
	[Desc("The condition to grant to self while waiting for crew member to enter.")]
	public readonly string? LoadingCondition = null;

	[GrantedConditionReference]
	[Desc("The condition to grant to self while crew members are entering.",
		"Condition can stack with multiple crew members.")]
	public readonly string? LoadedCondition = null;

	[ActorReference(dictionaryReference: LintDictionaryReference.Keys)]
	[Desc("Conditions to grant when specified actors are loaded inside the building.",
		"A dictionary of [actor name]: [condition].")]
	public readonly Dictionary<string, string> CrewMemberConditions = new();

	[GrantedConditionReference]
	public IEnumerable<string> LinterCrewMemberConditions => this.CrewMemberConditions.Values;

	public override object Create(ActorInitializer init) { return new BuildingCrew(init, this); }
}

public class BuildingCrew : ConditionalTrait<BuildingCrewInfo>, IIssueOrder, IResolveOrder, IOrderVoice,
	INotifyOwnerChanged, INotifySold, INotifyActorDisposing, IIssueDeployOrder,
	INotifyCreated, INotifyKilled, ITransformActorInitModifier
{
	private readonly Actor self;
	private readonly List<Actor> crewMembers = new();
	private readonly HashSet<Actor> reserves = new();
	private readonly Dictionary<string, Stack<int>> crewMemberTokens = new();
	private readonly Lazy<IFacing> facing;
	private readonly bool checkTerrainType;
	private int loadingToken = Actor.InvalidConditionToken;
	private readonly Stack<int> loadedTokens = new();
	private bool initialised;
	private readonly CachedTransform<CPos, IEnumerable<CPos>> currentAdjacentCells;

	public IEnumerable<CPos> CurrentAdjacentCells => this.currentAdjacentCells.Update(this.self.Location);

	public IReadOnlyCollection<Actor> CrewMembers => this.crewMembers;
	public int MemberCount => this.crewMembers.Count;

	public BuildingCrew(ActorInitializer init, BuildingCrewInfo info)
		: base(info)
	{
		this.self = init.Self;
		this.checkTerrainType = info.UnloadTerrainTypes.Count > 0;

		this.currentAdjacentCells = new CachedTransform<CPos, IEnumerable<CPos>>(loc =>
			Util.AdjacentCells(this.self.World, Target.FromActor(this.self)).Where(c => loc != c));

		//var runtimeCargoInit = init.GetOrDefault<RuntimeCargoInit>(info);
		//var cargoInit = init.GetOrDefault<CargoInit>(info);
		//if (runtimeCargoInit != null)
		//{
		//	this.cargo = runtimeCargoInit.Value.ToList();
		//}
		//else if (cargoInit != null)
		//{
		//	foreach (var u in cargoInit.Value)
		//	{
		//		var unit = this.self.World.CreateActor(false, u.ToLowerInvariant(),
		//			new TypeDictionary { new OwnerInit(this.self.Owner) });

		//		this.cargo.Add(unit);
		//	}
		//}
		//else
		//{
		//	foreach (var u in info.InitialUnits)
		//	{
		//		var unit = this.self.World.CreateActor(false, u.ToLowerInvariant(),
		//			new TypeDictionary { new OwnerInit(this.self.Owner) });

		//		this.cargo.Add(unit);
		//	}
		//}

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

			if (!string.IsNullOrEmpty(this.Info.LoadedCondition))
				this.loadedTokens.Push(self.GrantCondition(this.Info.LoadedCondition));
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

			yield return new DeployOrderTargeter("Unload", 10,
				() => this.CanUnload() ? this.Info.UnloadCursor : this.Info.UnloadBlockedCursor);
		}
	}

	public Order? IssueOrder(Actor self, IOrderTargeter order, in Target target, bool queued)
	{
		if (order.OrderID == "Unload")
			return new Order(order.OrderID, self, queued);

		return null;
	}

	Order IIssueDeployOrder.IssueDeployOrder(Actor self, bool queued)
	{
		return new Order("Unload", self, queued);
	}

	bool IIssueDeployOrder.CanIssueDeployOrder(Actor self, bool queued) { return true; }

	public void ResolveOrder(Actor self, Order order)
	{
		if (order.OrderString == "Unload")
		{
			if (!order.Queued && !this.CanUnload())
				return;

			self.QueueActivity(order.Queued, new CrewExit(self, this.Info.LoadRange));
		}
	}

	public bool CanUnload(BlockedByActor check = BlockedByActor.None)
	{
		if (this.IsTraitDisabled)
			return false;

		if (this.checkTerrainType)
		{
			var terrainType = this.self.World.Map.GetTerrainInfo(this.self.Location).Type;

			if (!this.Info.UnloadTerrainTypes.Contains(terrainType))
				return false;
		}

		return !this.IsEmpty() && this.CurrentAdjacentCells != null && this.CurrentAdjacentCells.Any(c => this.CrewMembers.Any(p => !p.IsDead && p.Trait<IPositionable>().CanEnterCell(c, null, check)));
	}

	public bool CanEnter(Actor a)
	{
		return !this.IsTraitDisabled && (this.reserves.Contains(a) || this.HasSpace());
	}

	internal bool ReserveSpace(Actor a)
	{
		if (this.reserves.Contains(a))
			return true;

		if (!this.HasSpace())
			return false;

		if (this.loadingToken == Actor.InvalidConditionToken)
			this.loadingToken = this.self.GrantCondition(this.Info.LoadingCondition);

		this.reserves.Add(a);

		return true;
	}

	internal void UnreserveSpace(Actor a)
	{
		if (!this.reserves.Contains(a) || this.self.IsDead)
			return;

		this.reserves.Remove(a);

		if (this.loadingToken != Actor.InvalidConditionToken)
			this.loadingToken = this.self.RevokeCondition(this.loadingToken);
	}

	public string? VoicePhraseForOrder(Actor self, Order order)
	{
		if (order.OrderString != "Unload" || this.IsEmpty() || !self.HasVoice(this.Info.UnloadVoice))
			return null;

		return this.Info.UnloadVoice;
	}

	public bool HasSpace() { return this.crewMembers.Count + this.reserves.Count + 1 <= this.Info.MaxPopulation; }
	public bool IsEmpty() { return this.crewMembers.Count == 0; }

	public Actor Peek() { return this.crewMembers.Last(); }

	public Actor Unload(Actor self, Actor? crewMember = null)
	{
		crewMember ??= this.crewMembers.Last();
		if (!this.crewMembers.Remove(crewMember))
			throw new ArgumentException("Attempted to unload an actor that is not a crew member.");

		this.SetCrewMemberFacing(crewMember);

		foreach (var npe in self.TraitsImplementing<INotifyCrewMemberExited>())
			npe.OnCrewMemberExited(self, crewMember);

		foreach (var nec in crewMember.TraitsImplementing<INotifyExitedBuildingCrew>())
			nec.OnExitedBuildingCrew(crewMember, self);

		var c = crewMember.Trait<CrewMember>();
		c.BuildingCrew = null;

		if (this.crewMemberTokens.TryGetValue(crewMember.Info.Name, out var crewMemberToken) && crewMemberToken.Count > 0)
			self.RevokeCondition(crewMemberToken.Pop());

		if (this.loadedTokens.Count > 0)
			self.RevokeCondition(this.loadedTokens.Pop());

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

	public void Load(Actor self, Actor crewMember)
	{
		this.crewMembers.Add(crewMember);
		if (this.reserves.Contains(crewMember))
		{
			this.reserves.Remove(crewMember);

			if (this.loadingToken != Actor.InvalidConditionToken)
				this.loadingToken = self.RevokeCondition(this.loadingToken);
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

		if (!string.IsNullOrEmpty(this.Info.LoadedCondition))
			this.loadedTokens.Push(self.GrantCondition(this.Info.LoadedCondition));
	}

	void INotifyKilled.Killed(Actor self, AttackInfo e)
	{
		if (this.Info.EjectOnDeath)
			while (!this.IsEmpty() && this.CanUnload(BlockedByActor.All))
			{
				var crewMember = this.Unload(self);
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
			this.SpawnCrewMember(this.Unload(self));
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

	void ITransformActorInitModifier.ModifyTransformActorInit(Actor self, TypeDictionary init)
	{
		init.Add(new RuntimeCargoInit(this.Info, this.CrewMembers.ToArray()));
	}
}

//public class RuntimeCargoInit : ValueActorInit<Actor[]>, ISuppressInitExport
//{
//	public RuntimeCargoInit(TraitInfo info, Actor[] value)
//		: base(info, value) { }
//}

//public class CargoInit : ValueActorInit<string[]>
//{
//	public CargoInit(TraitInfo info, string[] value)
//		: base(info, value) { }
//}