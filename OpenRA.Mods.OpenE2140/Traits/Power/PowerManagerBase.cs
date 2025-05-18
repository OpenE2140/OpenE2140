#region Copyright & License Information

/*
 * Copyright (c) The OpenE2140 Developers and Contributors
 * This file is part of OpenE2140, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */

#endregion

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

	protected readonly Dictionary<Actor, Power> Powers = [];

	public int Power { get; private set; }
	public int PowerGenerated { get; private set; }
	public int PowerConsumed { get; private set; }

	private long lastAdviceTime;

	protected PowerManagerBase(Actor self, PowerManagerBaseInfo info)
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
