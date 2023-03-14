#region Copyright & License Information

/*
 * Copyright 2007-2023 The OpenE2140 Developers (see AUTHORS)
 * This file is part of OpenE2140, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */

#endregion

using JetBrains.Annotations;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;

namespace OpenRA.Mods.OpenE2140.Traits.Construction;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("Attach to all production buildings with elevator production instead of default ProductionQueue")]
public class ElevatorProductionQueueInfo : ProductionQueueInfo
{
	public override object Create(ActorInitializer init)
	{
		return new ElevatorProductionQueue(init, this);
	}
}

public class ElevatorProductionQueue : ProductionQueue
{
	public ElevatorProductionQueue(ActorInitializer init, ProductionQueueInfo info)
		: base(init, info)
	{
	}

	protected override void BeginProduction(ProductionItem item, bool hasPriority)
	{
		var unit = this.Actor.World.Map.Rules.Actors[item.Item];
		var playerPower = this.Actor.Owner.PlayerActor.TraitOrDefault<PowerManager>();

		var currentItem = new ProductionItem(
			this,
			item.Item,
			item.TotalCost,
			playerPower,
			() => this.Actor.World.AddFrameEndTask(
				_ =>
				{
					if (!this.Queue.Any(i => i.Done && i.Item == unit.Name))
						return;

					this.BuildUnit(unit);
				}
			)
		);

		base.BeginProduction(currentItem, hasPriority);
	}

	protected override bool BuildUnit(ActorInfo unit)
	{
		var mostLikelyProducerTrait = this.MostLikelyProducer().Trait as ElevatorProduction;

		if (!this.Actor.IsInWorld || this.Actor.IsDead || mostLikelyProducerTrait == null)
		{
			this.CancelProduction(unit.Name, 1);

			return false;
		}

		if (mostLikelyProducerTrait.State != ElevatorProduction.AnimationState.Closed)
			return false;

		var inits = new TypeDictionary { new OwnerInit(this.Actor.Owner), new FactionInit(BuildableInfo.GetInitialFaction(unit, this.Faction)) };

		var bi = unit.TraitInfo<BuildableInfo>();
		var type = this.developerMode.AllTech ? this.Info.Type : bi.BuildAtProductionType ?? this.Info.Type;
		var item = this.Queue.First(i => i.Done && i.Item == unit.Name);

		return !mostLikelyProducerTrait.IsTraitPaused && mostLikelyProducerTrait.Produce(this.Actor, unit, type, inits, item.TotalCost);
	}

	public void UnitCompleted(Actor actor)
	{
		if (actor is null)
			throw new ArgumentNullException(nameof(actor));

		var done = this.Queue.FirstOrDefault(p => p.Done && p.Item == actor.Info.Name);

		if (done == null)
			return;

		this.EndProduction(done);

		var rules = this.Actor.World.Map.Rules;
		Game.Sound.PlayNotification(rules, this.Actor.Owner, "Speech", this.Info.ReadyAudio, this.Actor.Owner.Faction.InternalName);
		TextNotificationsManager.AddTransientLine(this.Info.ReadyTextNotification, this.Actor.Owner);
	}

	protected override void TickInner(Actor self, bool allProductionPaused)
	{
		var traits = this.productionTraits.OfType<ElevatorProduction>().Where(p => !p.IsTraitDisabled && p.Info.Produces.Contains(this.Info.Type));
		var unpaused = traits.Where(a => a is { IsTraitPaused: false, State: ElevatorProduction.AnimationState.Closed });

		if (unpaused.Any())
			base.TickInner(self, allProductionPaused);
	}
}
