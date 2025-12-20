using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.BotModules
{
	public interface IBotEconomyManager
	{
		bool HasSufficientEconomy();
	}

	public interface IBotMcuBaseBuilder
	{
		void RequestBuildingProduction(IBot bot, string actor);

		int RequestedProductionCount(IBot bot, string actor);

		int InProductionCount(IBot bot, string actor);
	}
}
