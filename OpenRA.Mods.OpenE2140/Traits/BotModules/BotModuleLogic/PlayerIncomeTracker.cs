using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.BotModules.BotModuleLogic;

public class PlayerIncomeTracker
{
	private readonly OpenRA.World world;
	private readonly PlayerResources playerResources;

	// High resolution (every second) record of earnings, limited to the last minute
	private readonly Queue<int> earnedSeconds = new(60);

	private int lastIncomeTick;

	public int Income { get; private set; }

	public int CurrentCash => this.playerResources.Cash;

	public PlayerIncomeTracker(OpenRA.World world, PlayerResources playerResources)
	{
		this.world = world;
		this.playerResources = playerResources;
	}

	public void Tick()
	{
		var tickDelta = this.world.WorldTick - this.lastIncomeTick;
		if (tickDelta * this.world.Timestep < 1000)
			return;

		this.lastIncomeTick = this.world.WorldTick;

		var lastEarned = this.earnedSeconds.Count > 59 ? this.earnedSeconds.Dequeue() : 0;

		this.Income = this.playerResources.Earned - lastEarned;
		this.earnedSeconds.Enqueue(this.playerResources.Earned);
	}
}
