using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Mods.OpenE2140.Traits.Resources;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.BotModules.BotModuleLogic;

internal class MineRefineryAssignment
{
	public Actor? Mine { get; set; }

	public Actor? Refinery { get; set; }

	public List<Actor> CrateTransporters { get; set; } = [];

	public int ExpectedCrateTransporterCount { get; init; }

	public void AssignCrateTransporters(List<Actor> freeCrateTransporters)
	{
		if (this.CrateTransporters.Count >= this.ExpectedCrateTransporterCount || freeCrateTransporters.Count == 0)
			return;

		for (var i = this.CrateTransporters.Count; i <= this.ExpectedCrateTransporterCount; i++)
		{
			if (freeCrateTransporters.Count == 0)
				break;

			var transporter = freeCrateTransporters[^1];
			freeCrateTransporters.RemoveAt(freeCrateTransporters.Count - 1);

			this.CrateTransporters.Add(transporter);
		}
	}

	public void OrderCrateTransportersToWork(IBot bot, HashSet<Actor> availableCrateTransporters)
	{
		if (this.Mine == null || this.Refinery == null)
			return;

		// Process already assigned crate transporters
		foreach (var actor in this.CrateTransporters)
			ProcessCrateTransporter(actor);

		// Try assigning new crate transporter, if there's currently not enough of them
		for (var i = this.CrateTransporters.Count; i < this.ExpectedCrateTransporterCount; i++)
		{
			var crateTransporter = availableCrateTransporters.FirstOrDefault();
			if (crateTransporter == null)
				break; // no additional transporters available

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

			// TODO: handle Mine depletion
			if ((crateTransporter.HasCrate && routine.CurrentRefinery != this.Refinery) || actor.IsIdle)
				QueueDockOrder(actor, this.Refinery, false, [this.Mine]);
			else if ((!crateTransporter.HasCrate && routine.CurrentMine != this.Mine) || actor.IsIdle)
				QueueDockOrder(actor, this.Mine, false, [this.Refinery]);
		}

		void QueueDockOrder(Actor actor, Actor? target, bool isQueued, Actor[]? extraActors = null)
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
			var crateTransporter = this.CrateTransporters[i];
			if (crateTransporter.IsDead)
				this.CrateTransporters.RemoveAt(i);
		}
	}
}
