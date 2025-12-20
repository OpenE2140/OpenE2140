using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Mods.OpenE2140.Traits.BotModules.Extensions;
using OpenRA.Mods.OpenE2140.Traits.Resources;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.BotModules;

[TraitLocation(SystemActors.Player)]
[Desc("Manages economy for a bot.")]
public class EconomyManagerBotModuleInfo : ConditionalTraitInfo, NotBefore<IResourceLayerInfo>
{
	[ActorReference]
	[Desc("Actor types that are considered crate transporters. If crate transporters count drops below RefineryTypes count, a new crate transporters is built.",
		"Leave empty to disable crate transporters replacement. Currently only needed by crate transporter replacement system.")]
	public readonly HashSet<string> CrateTransporterTypes = [];

	[ActorReference]
	[Desc("Tells the AI what building types are considered refineries.")]
	public readonly HashSet<string> RefineryTypes = [];

	[ActorReference]
	[Desc("Tells the AI what building types are considered mines.")]
	public readonly HashSet<string> MineTypes = [];

	[Desc("Delays in ticks between each time the AI should expand their economy.")]
	public readonly List<int> EconomyExpansionDelays = [];

	[Desc("Interval (in ticks) between performing the module logic.")]
	public readonly int LogicInterval = 50;

	public override object Create(ActorInitializer init) { return new EconomyManagerBotModule(init.Self, this); }

	public override void RulesetLoaded(Ruleset rules, ActorInfo ai)
	{
		base.RulesetLoaded(rules, ai);

		if (this.MineTypes.Count == 0)
			throw new YamlException($"At least one actor type has to be defined in {nameof(this.MineTypes)}");

		if (this.RefineryTypes.Count == 0)
			throw new YamlException($"At least one actor type has to be defined in {nameof(this.RefineryTypes)}");

		if (this.CrateTransporterTypes.Count == 0)
			throw new YamlException($"At least one actor type has to be defined in {nameof(this.CrateTransporterTypes)}");
	}
}

public class EconomyManagerBotModule : ConditionalTrait<EconomyManagerBotModuleInfo>, IBotTick, INotifyActorDisposing, IBotEconomyManager
{
	private readonly OpenRA.World world;
	private readonly Player player;
	private readonly ActorIndex.OwnerAndNamesAndTrait<CrateTransporterInfo> crateTransporters;
	private readonly ActorIndex.OwnerAndNamesAndTrait<ResourceMineInfo> mines;
	private readonly ActorIndex.OwnerAndNamesAndTrait<ResourceRefineryInfo> refineries;

	private IBotRequestUnitProduction[] requestUnitProduction = [];
	private IBotMcuBaseBuilder[] mcuBaseBuilder = [];
	private McuDeployManagerBotModule? mcuDeployManager;

	private int logicTicks;
	private bool hasSufficientEconomy;

	public EconomyManagerBotModule(Actor self, EconomyManagerBotModuleInfo info)
		: base(info)
	{
		this.world = self.World;
		this.player = self.Owner;
		this.crateTransporters = new ActorIndex.OwnerAndNamesAndTrait<CrateTransporterInfo>(this.world, info.CrateTransporterTypes, this.player);
		this.mines = new ActorIndex.OwnerAndNamesAndTrait<ResourceMineInfo>(this.world, info.MineTypes, this.player);
		this.refineries = new ActorIndex.OwnerAndNamesAndTrait<ResourceRefineryInfo>(this.world, info.RefineryTypes, this.player);
	}

	protected override void Created(Actor self)
	{
		this.requestUnitProduction = this.player.PlayerActor.TraitsImplementing<IBotRequestUnitProduction>().ToArray();
		this.mcuBaseBuilder = this.player.PlayerActor.TraitsImplementing<IBotMcuBaseBuilder>().ToArray();
		this.mcuDeployManager = this.player.PlayerActor.TraitOrDefault<McuDeployManagerBotModule>();
	}

	protected override void TraitEnabled(Actor self)
	{
		// Avoid all AIs running their logic the same tick, randomize their initial scan delay.
		this.logicTicks = this.world.LocalRandom.Next(this.Info.LogicInterval);
	}

	void IBotTick.BotTick(IBot bot)
	{
		if (--this.logicTicks > 0)
			return;

		this.logicTicks = this.Info.LogicInterval;

		this.hasSufficientEconomy = this.Tick(bot);
	}

	private bool Tick(IBot bot)
	{
		var mcuBaseBuilder = this.mcuBaseBuilder.FirstEnabledTraitOrDefault();

		var mineCount = this.mines.Alive().Count();
		var refineryCount = this.refineries.Alive().Count();

		var currentEconomyLevel = Math.Min(mineCount, refineryCount);
		var targetEconomyLevel = this.Info.EconomyExpansionDelays.Count(t => t < this.world.WorldTick) + 1;

		var isEconomySufficient = currentEconomyLevel >= targetEconomyLevel;

		// Not enough mines -> build more
		var sufficientMineCount = mineCount >= targetEconomyLevel;
		if (!sufficientMineCount)
		{
			if (mcuBaseBuilder != null)
			{
				var mineMcu = this.world.GetMcuFromActor(this.Info.MineTypes.Random(this.world.LocalRandom));
				if (TryRequestProduction(bot, mcuBaseBuilder, mineMcu))
					return isEconomySufficient;
			}
		}

		// Not enough refineries -> build more
		var sufficientRefineryCount = refineryCount >= targetEconomyLevel;
		if (!sufficientRefineryCount)
		{
			if (mcuBaseBuilder != null)
			{
				var refinery = this.world.GetMcuFromActor(this.Info.RefineryTypes.Random(this.world.LocalRandom));
				if (TryRequestProduction(bot, mcuBaseBuilder, refinery))
					return isEconomySufficient;
			}
		}

		// Not enough crate transporters -> build more
		var unitBuilder = this.requestUnitProduction.FirstEnabledTraitOrDefault();
		if (unitBuilder != null && sufficientMineCount && sufficientRefineryCount)
		{
			var crateTransporterCount = this.crateTransporters.Alive().Count();
			var expectedCount = 2;
			var enoughTransporters = crateTransporterCount >= expectedCount;

			var shouldBuild = !enoughTransporters
				&& this.mines.Alive().Any()
				&& this.refineries.Alive().Any();
			if (shouldBuild)
			{
				// Build the best crate transporter that's possible to build currently.
				var crateTransporterType = this.Info.CrateTransporterTypes
					.LastOrDefault(t => unitBuilder.CanBuildUnit(this.player, t));

				if (crateTransporterType != null
					&& unitBuilder.RequestedProductionCount(bot, crateTransporterType) == 0
					&& unitBuilder.InProductionCount(this.player, crateTransporterType) == 0)
					unitBuilder.RequestUnitProduction(bot, crateTransporterType);

				isEconomySufficient = true;
			}
			else if (!enoughTransporters)
				isEconomySufficient = false;
		}

		// TODO: other checks

		return isEconomySufficient;

		bool TryRequestProduction(IBot bot, IBotMcuBaseBuilder mcuBaseBuilder, ActorInfo? mcu)
		{
			if (mcu != null
				&& mcuBaseBuilder.RequestedProductionCount(bot, mcu.Name) == 0
				&& mcuBaseBuilder.InProductionCount(bot, mcu.Name) == 0
				&& (this.mcuDeployManager == null || this.mcuDeployManager.UndeployedMcuCount(bot, mcu.Name) == 0))
			{
				mcuBaseBuilder.RequestBuildingProduction(bot, mcu.Name);
				return true;
			}

			return false;
		}
	}

	public bool HasSufficientEconomy()
	{
		return this.hasSufficientEconomy;
	}

	void INotifyActorDisposing.Disposing(Actor self)
	{
		this.crateTransporters.Dispose();
		this.refineries.Dispose();
		this.mines.Dispose();
	}
}
