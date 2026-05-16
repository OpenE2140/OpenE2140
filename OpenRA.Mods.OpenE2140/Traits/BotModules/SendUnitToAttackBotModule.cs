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

using System.Collections.Frozen;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;
using OpenRA.Mods.Common;
using OpenRA.Mods.OpenE2140.Extensions;

namespace OpenRA.Mods.OpenE2140.Traits.BotModules;

[Flags]
public enum AttackDistance
{
	Closest = 0,
	Furthest = 1,
	Random = 2
}

// Adopted from: https://github.com/OpenHV/OpenHV/blob/main/OpenRA.Mods.HV/Traits/BotModules/SendUnitToAttackBotModule.cs
[TraitLocation(SystemActors.Player)]
[Desc("Bot logic for units that should not be sent with a regular squad, like suicide or subterranean units.")]
public class SendUnitToAttackBotModuleInfo : ConditionalTraitInfo
{
	[Desc("Actors used for attack, and their base desire provided for attack desire.",
		"When desire reach 100, AI will send them to attack.")]
	public readonly FrozenDictionary<string, int> ActorTypesAndAttackDesire = FrozenDictionary<string, int>.Empty;

	[Desc("Target types that can be targeted.")]
	public readonly BitSet<TargetableType> ValidTargets = new("Structure");

	[Desc("Target types that can't be targeted.")]
	public readonly BitSet<TargetableType> InvalidTargets;

	[Desc("Should attack the furthest or closest target. Possible values are Closest, Furthest, Random")]
	public readonly AttackDistance AttackDistance = AttackDistance.Closest;

	[Desc("Attack order name.")]
	public readonly string AttackOrderName = "Attack";

	[Desc("Find target and try attack target in this interval.")]
	public readonly int ScanTick = 463;

	[Desc("The total attack desire increases by this amount per scan",
		"Note: When there is no attack unit, the total attack desire will return to 0.")]
	public readonly int AttackDesireIncreasedPerScan = 10;

	public override object Create(ActorInitializer init) { return new SendUnitToAttackBotModule(init.Self, this); }
}

public class SendUnitToAttackBotModule : ConditionalTrait<SendUnitToAttackBotModuleInfo>, IBotTick
{
	private readonly OpenRA.World world;
	private readonly Player player;
	private readonly ActorIndex.OwnerAndNamesAndTrait<IPositionableInfo> attackActors;
	private readonly List<Actor> availableTargets = [];
	private int minAssignRoleDelayTicks;
	private int updateTargetsTicks;
	private int desireIncreased;

	public SendUnitToAttackBotModule(Actor self, SendUnitToAttackBotModuleInfo info)
		: base(info)
	{
		this.world = self.World;
		this.player = self.Owner;
		this.attackActors = new ActorIndex.OwnerAndNamesAndTrait<IPositionableInfo>(self.World, info.ActorTypesAndAttackDesire.Keys.ToList(), this.player);
		this.desireIncreased = 0;
	}

	protected override void TraitEnabled(Actor self)
	{
		// Avoid all AIs reevaluating assignments on the same tick, randomize their initial evaluation delay.
		this.minAssignRoleDelayTicks = this.world.LocalRandom.Next(0, this.Info.ScanTick);
		this.updateTargetsTicks = this.world.LocalRandom.Next(0, this.Info.ScanTick);
	}

	void IBotTick.BotTick(IBot bot)
	{
		if (--this.updateTargetsTicks <= 0)
		{
			this.updateTargetsTicks = this.Info.ScanTick;
			this.UpdateTargetActors();
		}

		if (--this.minAssignRoleDelayTicks <= 0)
		{
			this.minAssignRoleDelayTicks = 30;
			this.AssignTargets(bot);
		}
	}

	private void AssignTargets(IBot bot)
	{
		var attackDesire = 0;
		var actors = this.attackActors.Alive()
			.Where(a =>
			{
				if (this.Info.ActorTypesAndAttackDesire.TryGetValue(a.Info.Name, out var desire))
				{
					attackDesire += desire;
					return a.IsIdle || a.CurrentActivity is FlyIdle;
				}

				return false;
			})
			.ToList();

		if (actors.Count == 0)
		{
			this.desireIncreased = 0;
			return;
		}

		this.desireIncreased += this.Info.AttackDesireIncreasedPerScan;

		if (this.desireIncreased + attackDesire < 100)
			return;

		if (this.availableTargets.Count == 0)
			return;

		IEnumerable<Actor> targets = this.availableTargets;
		switch (this.Info.AttackDistance)
		{
			case AttackDistance.Closest:
				targets = targets.OrderBy(a => (a.CenterPosition - actors[0].CenterPosition).HorizontalLengthSquared);
				break;
			case AttackDistance.Furthest:
				targets = targets.OrderByDescending(a => (a.CenterPosition - actors[0].CenterPosition).HorizontalLengthSquared);
				break;
			case AttackDistance.Random:
				targets = targets.Shuffle(this.world.LocalRandom);
				break;
		}

		foreach (var t in targets)
		{
			var orderedActors = new List<Actor>();

			foreach (var a in actors)
			{
				if (!a.Info.HasTraitInfo<AircraftInfo>())
				{
					var mobile = a.TraitOrDefault<Mobile>();
					if (mobile?.PathFinder.PathExistsForLocomotor(mobile.Locomotor, a.Location, t.Location) != true)
						continue;
				}

				orderedActors.Add(a);
			}

			actors.RemoveAll(orderedActors.Contains);

			if (orderedActors.Count > 0)
			{
				var groupedActors = orderedActors.ToArray();
				bot.QueueOrder(new Order(this.Info.AttackOrderName, null, Target.FromActor(t), false, groupedActors: groupedActors));
				//bot.QueueOrder(new Order("AttackMove", null, Target.FromActor(groupedActors[0]), true, groupedActors: groupedActors));
			}

			if (actors.Count == 0)
				break;
		}
	}

	private void UpdateTargetActors()
	{
		var enemyPlayers = this.world.Players
			.Where(p => p.RelationshipWith(this.player) == PlayerRelationship.Enemy && p.WinState != WinState.Lost)
			.ToHashSet();
		if (enemyPlayers.Count == 0)
			return;

		this.availableTargets.Clear();
		this.availableTargets.AddRange(this.world.Actors.Where(a =>
		{
			if (a?.IsDead != false || !a.IsInWorld || !enemyPlayers.Contains(a.Owner))
				return false;

			var t = a.GetAllTargetTypes();

			if (!this.Info.ValidTargets.Overlaps(t) || this.Info.InvalidTargets.Overlaps(t))
				return false;

			var hasModifier = false;
			var visModifiers = a.TraitsImplementing<IVisibilityModifier>();
			foreach (var v in visModifiers)
			{
				if (v.IsVisible(a, this.player))
					return true;

				hasModifier = true;
			}

			return !hasModifier;
		}));
	}
}
