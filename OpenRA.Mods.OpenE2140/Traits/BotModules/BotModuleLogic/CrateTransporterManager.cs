using OpenRA.Mods.Common;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Mods.OpenE2140.Traits.Resources;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.BotModules.BotModuleLogic;

public class CrateTransporterManager
{
	private readonly List<MineRefineryAssignment> mineRefineryAssignments = [];
	private readonly ActorIndex.OwnerAndNamesAndTrait<ResourceMineInfo> mines;
	private readonly ActorIndex.OwnerAndNamesAndTrait<ResourceRefineryInfo> refineries;
	private readonly ActorIndex.OwnerAndNamesAndTrait<CrateTransporterInfo> crateTransporters;
	private readonly AssignmentDirtyCheck assignmentDirtyCheck = new AssignmentDirtyCheck();
	private readonly EconomyManagerBotModuleInfo info;

	public CrateTransporterManager(ActorIndex.OwnerAndNamesAndTrait<ResourceMineInfo> mines, ActorIndex.OwnerAndNamesAndTrait<ResourceRefineryInfo> refineries, ActorIndex.OwnerAndNamesAndTrait<CrateTransporterInfo> crateTransporters, EconomyManagerBotModuleInfo info)
	{
		this.mines = mines;
		this.refineries = refineries;
		this.crateTransporters = crateTransporters;
		this.info = info;
	}

	internal IReadOnlyList<MineRefineryAssignment> MineRefineryAssignments => this.mineRefineryAssignments;

	public void Tick(IBot bot)
	{
		this.UpdateMineRefineryAssignments();
		this.OrderCrateTransporterToWork(bot);
	}

	private void UpdateMineRefineryAssignments()
	{
		var aliveMines = this.mines.Alive().ToList();
		var aliveRefineries = this.refineries.Alive().ToList();

		if (!this.assignmentDirtyCheck.UpdateDirtyState(aliveMines, aliveRefineries))
			return;

		this.RebuildAssignments(aliveMines, aliveRefineries);
	}

	private void RebuildAssignments(List<Actor> aliveMines, List<Actor> aliveRefineries)
	{
		var allCrateTransporters = this.mineRefineryAssignments
			.SelectMany(a => a.CrateTransporters)
			.Where(t => !t.IsDead)
			.ToList();

		this.mineRefineryAssignments.Clear();

		var pairs = FindClosestPairs(aliveMines, aliveRefineries);
		var pairedMines = new HashSet<Actor>();
		var pairedRefineries = new HashSet<Actor>();

		foreach (var (mine, refinery) in pairs)
		{
			pairedMines.Add(mine);
			pairedRefineries.Add(refinery);

			this.mineRefineryAssignments.Add(new MineRefineryAssignment
			{
				Mine = mine,
				Refinery = refinery,
				ExpectedCrateTransporterCount = this.info.CrateTransporterPerRefineryMinePair
			});
		}

		this.RedistributeCrateTransporters(allCrateTransporters);
	}

	private static List<(Actor Mine, Actor Refinery)> FindClosestPairs(ICollection<Actor> mines, ICollection<Actor> refineries)
	{
		var result = new List<(Actor Mine, Actor Refinery)>();
		var usedMines = new HashSet<Actor>();
		var usedRefineries = new HashSet<Actor>();

		var pairs = mines
			.SelectMany(m => refineries.Select(r => (Mine: m, Refinery: r, Dist: (m.Location - r.Location).Length)))
			.OrderBy(p => p.Dist)
			.ToList();

		foreach (var (mine, refinery, _) in pairs)
		{
			if (usedMines.Contains(mine) || usedRefineries.Contains(refinery))
				continue;

			usedMines.Add(mine);
			usedRefineries.Add(refinery);
			result.Add((mine, refinery));
		}

		return result;
	}

	private void RedistributeCrateTransporters(List<Actor> crateTransporters)
	{
		if (this.mineRefineryAssignments.Count == 0)
			return;

		foreach (var crateTransporter in crateTransporters)
		{
			var bestAssignment = this.mineRefineryAssignments
				.Where(a => a.Mine != null)
				.MinBy(a => (a.Mine!.Location - crateTransporter.Location).Length);
			bestAssignment?.TryAddCrateTransporter(crateTransporter);
		}
	}

	private void OrderCrateTransporterToWork(IBot bot)
	{
		var assignedCrateTransporters = this.mineRefineryAssignments.SelectMany(a => a.CrateTransporters).ToHashSet();
		var availableCrateTransporters = this.crateTransporters.Alive().Except(assignedCrateTransporters).ToHashSet();

		foreach (var assignment in this.mineRefineryAssignments)
			assignment.OrderCrateTransportersToWork(bot, availableCrateTransporters);
	}

	private class AssignmentDirtyCheck
	{
		private HashSet<Actor> lastKnownMines = [];
		private HashSet<Actor> lastKnownRefineries = [];

		public bool UpdateDirtyState(IEnumerable<Actor> mines, IEnumerable<Actor> refineries)
		{
			var minesSet = mines.ToHashSet();
			var refineriesSet = refineries.ToHashSet();

			var isDirty = false;
			if (!minesSet.SetEquals(this.lastKnownMines))
				isDirty = true;

			if (!isDirty && !refineriesSet.SetEquals(this.lastKnownRefineries))
				isDirty = true;

			if (isDirty)
			{
				this.lastKnownMines = minesSet;
				this.lastKnownRefineries = refineriesSet;
			}

			return isDirty;
		}
	}
}
