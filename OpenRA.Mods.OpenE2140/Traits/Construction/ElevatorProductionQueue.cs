using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;

namespace OpenRA.Mods.OpenE2140.Traits.Construction
{
	[Desc("Attach to all production buildings with elevator production")]
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
			var unit = Actor.World.Map.Rules.Actors[item.Item];
			var playerPower = Actor.Owner.PlayerActor.TraitOrDefault<PowerManager>();
			var currentItem = new ProductionItem(this, item.Item, item.TotalCost, playerPower, () => this.Actor.World.AddFrameEndTask(_ =>
			{
				// Make sure the item hasn't been invalidated between the ProductionItem ticking and this FrameEndTask running
				if (!Queue.Any(i => i.Done && i.Item == unit.Name))
					return;

				BuildUnit(unit);
			}));
			base.BeginProduction(currentItem, hasPriority);

			// Make sure the item hasn't been invalidated between the ProductionItem ticking and this FrameEndTask running
			//if (!Queue.Any(i => i.Done && i.Item == unit.Name))
			//	return;

			//var isBuilding = unit.HasTraitInfo<BuildingInfo>();
			//if (isBuilding && !hasPlayedSound)
			//{
			//	hasPlayedSound = Game.Sound.PlayNotification(rules, self.Owner, "Speech", Info.ReadyAudio, self.Owner.Faction.InternalName);
			//	TextNotificationsManager.AddTransientLine(Info.ReadyTextNotification, self.Owner);
			//}
			//else if (!isBuilding)
			//{
			//	if (BuildUnit(unit))
			//	{
			//		Game.Sound.PlayNotification(rules, self.Owner, "Speech", Info.ReadyAudio, self.Owner.Faction.InternalName);
			//		TextNotificationsManager.AddTransientLine(Info.ReadyTextNotification, self.Owner);
			//	}
			//	else if (!hasPlayedSound && time > 0)
			//	{
			//		hasPlayedSound = Game.Sound.PlayNotification(rules, self.Owner, "Speech", Info.BlockedAudio, self.Owner.Faction.InternalName);
			//		TextNotificationsManager.AddTransientLine(Info.BlockedTextNotification, self.Owner);
			//	}
		}

		protected override bool BuildUnit(ActorInfo unit)
		{
			var mostLikelyProducerTrait = MostLikelyProducer().Trait as ElevatorProduction;

			// Cannot produce if I'm dead or trait is disabled
			if (!Actor.IsInWorld || Actor.IsDead || mostLikelyProducerTrait == null)
			{
				CancelProduction(unit.Name, 1);
				return false;
			}

			if (mostLikelyProducerTrait.State == ElevatorProduction.AnimationState.Closed)
			{
				var inits = new TypeDictionary
				{
					new OwnerInit(Actor.Owner),
					new FactionInit(BuildableInfo.GetInitialFaction(unit, Faction))
				};

				var bi = unit.TraitInfo<BuildableInfo>();
				var type = developerMode.AllTech ? Info.Type : (bi.BuildAtProductionType ?? Info.Type);
				var item = Queue.First(i => i.Done && i.Item == unit.Name);
				if (!mostLikelyProducerTrait.IsTraitPaused && mostLikelyProducerTrait.Produce(Actor, unit, type, inits, item.TotalCost))
				{
					return true;
				}

				return false;
			}

			return false;
		}

		public void UnitCompleted(ElevatorProduction elevatorProduction, Actor actor)
		{
			// Question: can there actually be production items that don't match produced actor? (maybe for ParallelProductionQueue?)
			var done = this.Queue.FirstOrDefault(p => p.Done && p.Item == actor.Info.Name);
			if (done != null)
			{
				EndProduction(done);

				var rules = Actor.World.Map.Rules;
				Game.Sound.PlayNotification(rules, Actor.Owner, "Speech", Info.ReadyAudio, Actor.Owner.Faction.InternalName);
				TextNotificationsManager.AddTransientLine(Info.ReadyTextNotification, Actor.Owner);
			}
		}

		protected override void TickInner(Actor self, bool allProductionPaused)
		{
			var traits = productionTraits
				.OfType<ElevatorProduction>()
				.Where(p => !p.IsTraitDisabled && p.Info.Produces.Contains(Info.Type));
			var unpaused = traits.Where(a => !a.IsTraitPaused && a.State == ElevatorProduction.AnimationState.Closed);
			if (unpaused.Any())
			{
				base.TickInner(self, allProductionPaused);
			}
			else
			{

			}
		}
	}
}
