using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Traits.Research;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.BotModules;

[TraitLocation(SystemActors.Player)]
[Desc("Manages AI technology research.")]
public class ResearchBotModuleInfo : ConditionalTraitInfo
{
	[Desc("Delay between each research")]
	public readonly int ResearchDelay = 20;

	public override object Create(ActorInitializer init)
	{
		return new ResearchBotModule(init.Self, this);
	}
}

public class ResearchBotModule : ConditionalTrait<ResearchBotModuleInfo>, IBotTick
{
	private readonly Player player;

	private Research.Research? research;
	private int researchTick;
	private bool researchCompleted;

	public ResearchBotModule(Actor self, ResearchBotModuleInfo info)
		: base(info)
	{
		this.player = self.Owner;
	}

	protected override void Created(Actor self)
	{
		this.research = this.player.PlayerActor.Trait<Research.Research>();
		this.researchTick = this.Info.ResearchDelay;
	}

	void IBotTick.BotTick(IBot bot)
	{
		if (this.research == null)
			return;

		if (this.research.Current != null || this.researchCompleted)
			return;

		if (--this.researchTick > 0)
			return;

		// Wait until research center is constructed.
		if (!this.research.CanResearch)
		{
			this.researchTick = this.Info.ResearchDelay;
			return;
		}

		var nextTechnologyId = this.GetNextTechnology()?.ResearchableInfo.Id;
		if (nextTechnologyId == null)
		{
			this.researchCompleted = true;
			return;
		}

		//AIUtils.BotDebug("{0} decided to research {1}", this.player, nextTechnologyId);

		this.player.World.IssueOrder(new Order(Research.Research.StartResearchOrder, this.player.PlayerActor, false) { TargetString = nextTechnologyId });

		this.researchTick = this.Info.ResearchDelay;
	}

	private Technology? GetNextTechnology()
	{
		return this.research?.Technologies
			.OrderBy(t => t.ResearchableInfo.Level)
			.FirstOrDefault(t => t.IsResearchable && !t.IsResearched);
	}
}
