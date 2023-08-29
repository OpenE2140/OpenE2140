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

using JetBrains.Annotations;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Production;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("Attach to all production buildings with animated exit production instead of default ProductionQueue")]
public class AnimatedExitProductionQueueInfo : ProductionQueueInfo, Requires<AnimatedExitProductionInfo>
{
	public override object Create(ActorInitializer init)
	{
		return new AnimatedExitProductionQueue(init, this);
	}
}

public class AnimatedExitProductionQueue : ProductionQueue
{
	private readonly Lazy<AnimatedExitProduction[]> animatedExitProductionTraits;

	private IEnumerable<AnimatedExitProduction> EnabledProductionTraits => this.animatedExitProductionTraits.Value.Where(e => !e.IsTraitDisabled);

	public AnimatedExitProductionQueue(ActorInitializer init, ProductionQueueInfo info)
		: base(init, info)
	{
		this.animatedExitProductionTraits = Exts.Lazy(
			() => this.productionTraits.OfType<AnimatedExitProduction>().Where(p => p.Info.Produces.Contains(this.Info.Type)).ToArray()
		);
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
		var mostLikelyProducerTrait = this.MostLikelyProducer().Trait as AnimatedExitProduction;

		if (!this.Actor.IsInWorld || this.Actor.IsDead || mostLikelyProducerTrait == null)
		{
			this.CancelProduction(unit.Name, 1);

			return false;
		}

		// If the *Production trait cannot build new unit at this precise moment, just wait a bit (ProductionQueue's ProductionItem will keep
		// calling OnComplete callback, until the item is removed from the Queue using ProductionQueue.EndProduction method).
		if (!mostLikelyProducerTrait.CanBuildUnitNow)
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
		TextNotificationsManager.AddTransientLine(this.Actor.Owner, this.Info.ReadyTextNotification);
	}

	protected override void CancelProduction(string itemName, uint numberToCancel)
	{
		var closed = this.EnabledProductionTraits.Where(a => a.State == AnimatedExitProduction.AnimationState.Closed);

		var currentItem = this.Queue.FirstOrDefault();
		var queuedCount = this.Queue.Count(i => i.Item == itemName);
		var isInfinite = this.Queue.Any(i => i.Item == itemName && i.Infinite);

		// If unit is currently being ejected (i.e. animated exit is not closed), we cannot cancel the last item (as this would refund paid cash)
		if (!closed.Any() && (currentItem == null || currentItem.Item == itemName))
		{
			numberToCancel = (uint)Math.Min(Math.Max(0, queuedCount - 1), numberToCancel);

			// If the item has Infinite flag, we need to cancel it, since that will remove the Infinite flag
			// and queue items to fill the infinite build limit (which is what player wanted).
			// The currently ejected unit will not be affected by this (since the item with Infinite flag will be preserved).
			if (isInfinite)
				++numberToCancel;
		}

		base.CancelProduction(itemName, numberToCancel);
	}

	protected override void PauseProduction(string itemName, bool paused)
	{
		var closed = this.EnabledProductionTraits.Where(a => a.State == AnimatedExitProduction.AnimationState.Closed);

		// Don't pause production if unit is being ejected (i.e. animated exit is open).
		if (closed.Any())
			base.PauseProduction(itemName, paused);
	}

	protected override void TickInner(Actor self, bool allProductionPaused)
	{
		var unpaused = this.EnabledProductionTraits.Where(a => a is { IsTraitPaused: false });

		if (unpaused.Any())
			base.TickInner(self, allProductionPaused);
	}

	/// <summary>
	/// Calculates production cost of <paramref name="unit"/> using <see cref="IProductionCostModifierInfo"/> modifiers for both unit and queue's actor.
	/// </summary>
	public override int GetProductionCost(ActorInfo unit)
	{
		var valued = unit.TraitInfoOrDefault<ValuedInfo>();

		if (valued == null)
			return 0;

		var modifiers = unit.TraitInfos<IProductionCostModifierInfo>()
			.Concat(this.Actor.Info.TraitInfos<IProductionCostModifierInfo>())
			.Select(t => t.GetProductionCostModifier(this.techTree, this.Info.Type));

		return Util.ApplyPercentageModifiers(valued.Cost, modifiers);
	}
}
