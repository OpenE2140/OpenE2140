using OpenRA.Mods.Common.Traits;
using OpenRA.Support;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.AI;

[Desc("Bot that uses BotModules. It's activated regardless of whether the client is actually bot or not.")]
[TraitLocation(SystemActors.Player)]
public sealed class ForcedModularBotInfo : TraitInfo, IBotInfo
{
	[FieldLoader.Require]
	[Desc("Internal id for this bot.")]
	public readonly string Type = string.Empty;

	[FluentReference]
	[Desc("Human-readable name this bot uses.")]
	public readonly string Name = string.Empty;

	[Desc("Minimum portion of pending orders to issue each tick (e.g. 5 issues at least 1/5th of all pending orders). " +
		"Excess orders remain queued for subsequent ticks.")]
	public readonly int MinOrderQuotientPerTick = 5;

	string IBotInfo.Type => this.Type;

	string IBotInfo.Name => this.Name;

	public override object Create(ActorInitializer init) { return new ForcedModularBot(this, init); }
}

public sealed class ForcedModularBot : ITick, IBot, INotifyDamage, INotifyCreated
{
	public bool IsEnabled;

	private readonly ForcedModularBotInfo info;
	private readonly OpenRA.World world;
	private readonly Queue<Order> orders = [];

	private readonly Player player;

	private IBotTick[] tickModules = [];
	private IBotRespondToAttack[] attackResponseModules = [];

	IBotInfo IBot.Info => this.info;
	Player IBot.Player => this.player;

	public ForcedModularBot(ForcedModularBotInfo info, ActorInitializer init)
	{
		this.info = info;
		this.world = init.World;
		this.player = init.Self.Owner;
	}

	void INotifyCreated.Created(Actor self)
	{
		this.ActivateBot(self.Owner);
	}

	private void ActivateBot(Player player)
	{
		// Bot logic is not allowed to affect world state, and can only act by issuing orders
		// These orders are recorded in the replay, so bots shouldn't be enabled during replays
		if (player.World.IsReplay)
			return;

		this.IsEnabled = true;
		this.tickModules = player.PlayerActor.TraitsImplementing<IBotTick>().ToArray();
		this.attackResponseModules = player.PlayerActor.TraitsImplementing<IBotRespondToAttack>().ToArray();
		foreach (var ibe in player.PlayerActor.TraitsImplementing<IBotEnabled>())
			ibe.BotEnabled(this);
	}

	void IBot.Activate(Player p)
	{
		this.ActivateBot(p);
	}

	void IBot.QueueOrder(Order order)
	{
		this.orders.Enqueue(order);
	}

	void ITick.Tick(Actor self)
	{
		if (!this.IsEnabled || self.World.IsLoadingGameSave)
			return;

		using (new PerfSample("bot_tick"))
		{
			Sync.RunUnsynced(Game.Settings.Debug.SyncCheckBotModuleCode, this.world, () =>
			{
				foreach (var t in this.tickModules)
					if (t.IsTraitEnabled())
						t.BotTick(this);
			});
		}

		var ordersToIssueThisTick = Math.Min((this.orders.Count + this.info.MinOrderQuotientPerTick - 1) / this.info.MinOrderQuotientPerTick, this.orders.Count);
		for (var i = 0; i < ordersToIssueThisTick; i++)
			this.world.IssueOrder(this.orders.Dequeue());
	}

	void INotifyDamage.Damaged(Actor self, AttackInfo e)
	{
		if (!this.IsEnabled || self.World.IsLoadingGameSave)
			return;

		using (new PerfSample("bot_attack_response"))
		{
			Sync.RunUnsynced(Game.Settings.Debug.SyncCheckBotModuleCode, this.world, () =>
			{
				foreach (var t in this.attackResponseModules)
					if (t.IsTraitEnabled())
						t.RespondToAttack(this, self, e);
			});
		}
	}
}
