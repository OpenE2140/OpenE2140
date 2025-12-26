using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.BotModules
{
	public interface IBotEconomyManager
	{
		bool HasSufficientEconomy();

		List<CPos> GetDeployCellsCandidates(Actor mcu, CPos? target);
	}

	public interface IBotMcuBaseBuilder
	{
		void RequestBuildingProduction(IBot bot, string actor);

		int RequestedProductionCount(IBot bot, string actor);

		int InProductionCount(IBot bot, string actor);
	}
}
