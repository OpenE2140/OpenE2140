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
public class PowerManagerInfo : TraitInfo, Requires<DeveloperModeInfo>
{
	[Desc("Interval (in milliseconds) at which to warn the player of low power.")]
	public readonly int AdviceInterval = 10000;

	[Desc("Interval (in ticks) at which to to cycle the power actors on low power.")]
	public readonly int PowerCycleInterval = 3;

	[Desc("Percentage of how much remaining energy can be used.")]
	public readonly int LowPowerPercent = 20;

	[NotificationReference("Speech")]
	public readonly string? SpeechNotification;

	public readonly string? TextNotification;

	public override object Create(ActorInitializer init)
	{
		return new PowerManager(init.Self, this);
	}
}

public class PowerManager : ITick
{
	private readonly PowerManagerInfo info;

	private readonly DeveloperMode devMode;

	private readonly Dictionary<Actor, Power> powers = new Dictionary<Actor, Power>();

	public int Power { get; private set; }
	public int PowerGenerated { get; private set; }
	public int PowerConsumed { get; private set; }

	private int firstPower;
	private long lastAdviceTime;

	public PowerManager(Actor self, PowerManagerInfo info)
	{
		this.info = info;

		this.devMode = self.Trait<DeveloperMode>();
	}

	public void Add(Actor actor, Power power)
	{
		this.powers.Add(actor, power);
	}

	public void Remove(Actor actor)
	{
		this.powers.Remove(actor);
	}

	void ITick.Tick(Actor self)
	{
		this.PowerGenerated = 0;
		this.PowerConsumed = 0;

		foreach (var power in this.powers.Values.Where(power => !power.IsTraitDisabled))
		{
			if (power.Info.Amount > 0)
				this.PowerGenerated += power.Info.Amount;
			else
				this.PowerConsumed += -power.Info.Amount;
		}

		this.Power = this.PowerGenerated - this.PowerConsumed;

		var remaining = this.PowerGenerated;

		if (this.devMode.UnlimitedPower)
			remaining = int.MaxValue;
		else if (this.Power < 0)
			remaining = remaining * this.info.LowPowerPercent / 100;

		var powered = 0;

		for (var i = 0; i < this.powers.Count; i++)
		{
			var (actor, power) = this.powers.ElementAt((this.firstPower + i) % this.powers.Count);

			if (power.IsTraitDisabled)
			{
				power.SetPowered(actor, false);

				continue;
			}

			var consume = -power.Info.Amount;

			if (consume < 0)
			{
				power.SetPowered(actor, true);

				continue;
			}

			remaining -= consume;

			power.SetPowered(actor, remaining >= 0);

			if (remaining >= 0)
				powered++;
		}

		if (remaining >= 0)
			return;

		if (self.World.WorldTick % this.info.PowerCycleInterval == 0)
			this.firstPower = (this.firstPower + powered) % this.powers.Count;

		if (Game.RunTime <= this.lastAdviceTime + this.info.AdviceInterval)
			return;

		Game.Sound.PlayNotification(self.World.Map.Rules, self.Owner, "Speech", this.info.SpeechNotification, self.Owner.Faction.InternalName);
		TextNotificationsManager.AddTransientLine(this.info.TextNotification, self.Owner);

		this.lastAdviceTime = Game.RunTime;
	}
}
