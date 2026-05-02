using OpenRA.Mods.Common;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Mods.OpenE2140.Traits.Resources;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.BotModules.BotModuleLogic;

public class CrateTransporterManager
{
	private readonly List<MineRefineryAssignment> mineRefineryAssignments = [];
	private readonly (List<Actor> Mines, List<Actor> Refineries) assignmentActorsDirtyCheck = ([], []);
	private readonly ActorIndex.OwnerAndNamesAndTrait<ResourceMineInfo> mines;
	private readonly ActorIndex.OwnerAndNamesAndTrait<ResourceRefineryInfo> refineries;
	private readonly ActorIndex.OwnerAndNamesAndTrait<CrateTransporterInfo> crateTransporters;
	private readonly EconomyManagerBotModuleInfo info;

	public CrateTransporterManager(ActorIndex.OwnerAndNamesAndTrait<ResourceMineInfo> mines, ActorIndex.OwnerAndNamesAndTrait<ResourceRefineryInfo> refineries, ActorIndex.OwnerAndNamesAndTrait<CrateTransporterInfo> crateTransporters, EconomyManagerBotModuleInfo info)
	{
		this.mines = mines;
		this.refineries = refineries;
		this.crateTransporters = crateTransporters;
		this.info = info;
	}

	internal IReadOnlyList<MineRefineryAssignment> MineRefineryAssignments => this.mineRefineryAssignments;

	public void OnSufficientEconomy()
	{
		// Force reassigning mines/refineries, in case crate transporters got out of sync.
		this.assignmentActorsDirtyCheck.Mines.Clear();
		this.assignmentActorsDirtyCheck.Refineries.Clear();
	}

	public void Tick(IBot bot)
	{
		this.UpdateMineRefineryAssignments();
		this.OrderCrateTransporterToWork(bot);
	}

	private void UpdateMineRefineryAssignments()
	{
		var unassignedMines = this.mines.Alive().ToHashSet();
		var unassignedRefineries = this.refineries.Alive().ToHashSet();

		var hasChanged = false;
		if (!unassignedMines.SetEquals(this.assignmentActorsDirtyCheck.Mines))
			hasChanged = true;

		if (!hasChanged && !unassignedRefineries.SetEquals(this.assignmentActorsDirtyCheck.Refineries))
			hasChanged = true;

		if (!hasChanged)
			return;

		this.assignmentActorsDirtyCheck.Mines.Clear();
		this.assignmentActorsDirtyCheck.Mines.AddRange(unassignedMines);
		this.assignmentActorsDirtyCheck.Refineries.Clear();
		this.assignmentActorsDirtyCheck.Refineries.AddRange(unassignedRefineries);

		if (unassignedMines.Count == 0 || unassignedRefineries.Count == 0)
		{
			this.mineRefineryAssignments.Clear();
			return;
		}

		// Create lookup of existing, valid assignment pairs to preserve existing connections
		var validAssignmentPairs = this.mineRefineryAssignments
			.Where(a => a.Mine?.IsDead == false && a.Refinery?.IsDead == false)
			.ToDictionary(a => (a.Mine, a.Refinery));
		this.mineRefineryAssignments.Clear();
		this.mineRefineryAssignments.EnsureCapacity(Math.Max(unassignedMines.Count, unassignedRefineries.Count));

		foreach (var mine in unassignedMines)
		{
			Actor? nearestRefinery = null;
			var maxSearchRadius = this.info.MaxRefineryDistance;
			for (var i = 0; i <= 3; i++)
			{
				var searchResult = FindNearestActor(unassignedRefineries, mine.Location, maxSearchRadius * i);
				if (searchResult?.actor == null)
				{
					++maxSearchRadius;
					continue;
				}

				nearestRefinery = searchResult.Value.actor;
				break;
			}

			if (nearestRefinery != null)
			{
				if (validAssignmentPairs.TryGetValue((mine, nearestRefinery), out var assignment))
				{
					assignment.RemoveInvalidCrateTransporters();
				}
				else
				{
					assignment = new MineRefineryAssignment
					{
						Mine = mine,
						Refinery = nearestRefinery,
						ExpectedCrateTransporterCount = this.info.CrateTransporterPerRefineryMinePair
					};
				}

				this.mineRefineryAssignments.Add(assignment);

				unassignedRefineries.Remove(nearestRefinery);
			}

			static (Actor actor, int distance)? FindNearestActor(IEnumerable<Actor> actors, CPos searchStart, int maxRadius)
			{
				return actors
					.Select(a => (actor: a, distance: (a.Location - searchStart).LengthSquared))
					.OrderBy(t => t.distance)
					.FirstOrDefault(a => a.distance <= maxRadius.PowerOf2());
			}
		}
	}

	private void OrderCrateTransporterToWork(IBot bot)
	{
		var availableCrateTransporters = this.crateTransporters.Alive().ToHashSet();
		if (availableCrateTransporters.Count == 0)
			return;

		// First pass: skip those crate transporters, which are already assigned
		foreach (var assignment in this.mineRefineryAssignments)
		{
			for (var i = assignment.CrateTransporters.Count - 1; i >= 0; i--)
			{
				var crateTransporter = assignment.CrateTransporters[i];

				availableCrateTransporters.Remove(crateTransporter);

				// Clean up any dead crate transporters
				if (crateTransporter.IsDead)
					assignment.CrateTransporters.RemoveAt(i);
			}
		}

		// Second pass: queue orders for crate transporters and assign those, which are free (i.e. currently unassigned)
		foreach (var assignment in this.mineRefineryAssignments)
		{
			assignment.OrderCrateTransportersToWork(bot, availableCrateTransporters);
		}
	}

}
