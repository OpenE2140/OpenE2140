using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.BotModules.Extensions;

public static class BotExtensions
{
	// TODO move to IBotRequestUnitProduction interface
	public static bool CanBuildUnit(
		this IBotRequestUnitProduction botRequestUnitProduction,
		Player player,
		string actorName)
	{
		var world = player.World;

		var queuesByCategory = AIUtils.FindQueuesByCategory(player);

		var actorInfo = world.Map.Rules.Actors[actorName];
		if (actorInfo == null)
			return false;

		var buildableInfo = actorInfo.TraitInfoOrDefault<BuildableInfo>();
		if (buildableInfo == null)
			return false;

		return buildableInfo.Queue.Any(pq => queuesByCategory[pq].FirstOrDefault()?.CanBuild(actorInfo) == true);
	}

	public static int InProductionCount(
		this IBotRequestUnitProduction botRequestUnitProduction,
		Player player,
		string actorName)
	{
		var world = player.World;

		var queuesByCategory = AIUtils.FindQueuesByCategory(player);

		var actorInfo = world.Map.Rules.Actors[actorName];
		if (actorInfo == null)
			return 0;

		var buildableInfo = actorInfo.TraitInfoOrDefault<BuildableInfo>();
		if (buildableInfo == null)
			return 0;

		return buildableInfo.Queue
			.SelectMany(pq => queuesByCategory[pq])
			.Sum(q => q.AllQueued().Count(i => i.Item == actorName));
	}
}
