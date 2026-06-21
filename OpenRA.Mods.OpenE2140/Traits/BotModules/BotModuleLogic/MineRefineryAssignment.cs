using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Mods.OpenE2140.Traits.Resources;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.BotModules.BotModuleLogic;

internal class MineRefineryAssignment
{
	public Actor? Mine { get; set; }

	public Actor? Refinery { get; set; }

	public List<Actor> CrateTransporters { get; } = [];

	public int ExpectedCrateTransporterCount { get; init; }

	public void OrderCrateTransportersToWork(IBot bot, HashSet<Actor> availableCrateTransporters)
	{
		if (this.Mine == null || this.Refinery == null)
			return;

		this.RemoveInvalidCrateTransporters();

		foreach (var actor in this.CrateTransporters)
			ProcessCrateTransporter(actor);

		while (this.CrateTransporters.Count < this.ExpectedCrateTransporterCount)
		{
			var crateTransporter = availableCrateTransporters
				.MinByOrDefault(t => (this.Mine.Location - t.Location).Length);
			if (crateTransporter == null)
				break;

			availableCrateTransporters.Remove(crateTransporter);
			this.CrateTransporters.Add(crateTransporter);
			ProcessCrateTransporter(crateTransporter);
		}

		void ProcessCrateTransporter(Actor actor)
		{
			if (!actor.TryGetTrait<CrateTransporter>(out var crateTransporter))
				return;

			if (!actor.TryGetTrait<CrateTransporterRoutine>(out var routine))
				return;

			var currentMine = routine.CurrentMine;
			var currentRefinery = routine.CurrentRefinery;

			if (currentMine?.IsDead == true || currentMine?.IsInWorld == false)
				currentMine = null;

			if (currentRefinery?.IsDead == true || currentRefinery?.IsInWorld == false)
				currentRefinery = null;

			if ((crateTransporter.HasCrate && currentRefinery != this.Refinery) || actor.IsIdle)
				QueueDockOrder(actor, this.Refinery, false, [this.Mine]);
			else if ((!crateTransporter.HasCrate && currentMine != this.Mine) || actor.IsIdle)
				QueueDockOrder(actor, this.Mine, false, [this.Refinery]);
		}

		void QueueDockOrder(Actor actor, Actor target, bool isQueued, Actor[]? extraActors = null)
		{
			var order = new Order(CrateTransporterRoutine.TransportCratesOrderID, actor, Target.FromActor(target), isQueued)
			{
				ExtraActors = extraActors ?? []
			};

			bot.QueueOrder(order);
		}
	}

	internal void RemoveInvalidCrateTransporters()
	{
		for (var i = this.CrateTransporters.Count - 1; i >= 0; i--)
		{
			if (this.CrateTransporters[i].IsDead)
				this.CrateTransporters.RemoveAt(i);
		}
	}

	internal void TryAddCrateTransporter(Actor crateTransporter)
	{
		if (this.CrateTransporters.Count < this.ExpectedCrateTransporterCount - 1)
			this.CrateTransporters.Add(crateTransporter);
	}
}
