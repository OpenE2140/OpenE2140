using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.BotModules
{
	public interface IBotEconomyManager
	{
		bool HasSufficientEconomy();

		List<DeployZone> GetDeployCellsCandidates(Actor mcu);
	}

	public class DeployZone
	{
		public List<CPos> CandidateCells { get; init; } = [];

		public CPos PreferredLocation { get; init; }
	}

	public interface IBotMcuBaseBuilder
	{
		void RequestBuildingProduction(IBot bot, string actor);

		int RequestedProductionCount(IBot bot, string actor);

		int InProductionCount(IBot bot, string actor);
	}

	public interface IBotMcuDeployManager
	{
		int UndeployedMcuCount(IBot bot, string mcuType);
	}

	public interface IBotMcuDeployment
	{
		void OrderedMcuToDeploy(IBot bot, Actor mcuActor, CPos deployLocation) { }

		void McuDeployed(IBot bot, Actor mcuActor, Actor buildingActor) { }

		void McuTransformed(IBot bot, Actor buildingActor) { }
	}
}
