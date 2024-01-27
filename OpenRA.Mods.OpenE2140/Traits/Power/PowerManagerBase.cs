using JetBrains.Annotations;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Power;

[TraitLocation(SystemActors.Player)]
[Desc("Earth specific variant of the PowerManager trait.")]
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public abstract class PowerManagerBaseInfo : TraitInfo, Requires<DeveloperModeInfo>
{
	[Desc("Interval (in milliseconds) at which to warn the player of low power.")]
	public readonly int AdviceInterval = 10000;

	[NotificationReference("Speech")]
	public readonly string? SpeechNotification;

	public readonly string? TextNotification;
}

public abstract class PowerManagerBase : ITick
{
	private readonly PowerManagerBaseInfo info;
	protected DeveloperMode DevMode { get; }

	protected readonly Dictionary<Actor, Power> Powers = new Dictionary<Actor, Power>();

	public int Power { get; private set; }
	public int PowerGenerated { get; private set; }
	public int PowerConsumed { get; private set; }

	private long lastAdviceTime;

	public PowerManagerBase(Actor self, PowerManagerBaseInfo info)
	{
		this.info = info;

		this.DevMode = self.Trait<DeveloperMode>();
	}

	public void Add(Actor actor, Power power)
	{
		this.Powers.Add(actor, power);
	}

	public void Remove(Actor actor)
	{
		this.Powers.Remove(actor);
	}

	void ITick.Tick(Actor self)
	{
		this.PowerGenerated = 0;
		this.PowerConsumed = 0;

		foreach (var power in this.Powers.Values.Where(power => !power.IsTraitDisabled))
		{
			if (power.Info.Amount > 0)
				this.PowerGenerated += power.Info.Amount;
			else
				this.PowerConsumed += -power.Info.Amount;
		}

		this.Power = this.PowerGenerated - this.PowerConsumed;

		var state = this.TickInner(self);
		if (state == PlayerPowerState.Ok)
			return;

		if (Game.RunTime <= this.lastAdviceTime + this.info.AdviceInterval)
			return;

		Game.Sound.PlayNotification(self.World.Map.Rules, self.Owner, "Speech", this.info.SpeechNotification, self.Owner.Faction.InternalName);
		TextNotificationsManager.AddTransientLine(self.Owner, this.info.TextNotification);

		this.lastAdviceTime = Game.RunTime;
	}

	protected abstract PlayerPowerState TickInner(Actor self);
}

public enum PlayerPowerState
{
	Ok,
	LowPower
}
