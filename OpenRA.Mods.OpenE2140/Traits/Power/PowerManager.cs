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
	public readonly int PowerCycleInterval = 5;

	[Desc("Percentage of how much generated energy can be used (for all buildings), when there's low power.")]
	public readonly int UsableEnergyWhenLowPowerPercent = 80;

	[Desc("Low power state: threshold for decreasing number of buildings powered in one power cycle")]
	public readonly int LowPowerThresholdDiff = 200;

	[Desc("Low power state: how many buildings can be powered in one power cycle. This number is baseline, when the power deficit is smallest.")]
	public readonly int LowPowerInitialGroupSize = 5;

	[Desc("Low power state: how many Power Plants can increase number of buildings in one power cycle",
		"Each Power Plant adds an extra building to total number of buildings, which can be powered in one power cycle. " +
		"This value is a limit, beyond which the number of buildings is not increased anymore.")]
	public readonly int LowPowerExtraGroupsLimit = 4;

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

	private int? currentLowPowerGroup;
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
			remaining = remaining * this.info.UsableEnergyWhenLowPowerPercent / 100;

		var powerPlantCount = this.powers.Values.Count(p => !p.IsTraitDisabled && p.Info.Amount > 0);

		var powered = 0;
		var lowPowered = new List<(Actor, Power)>(this.powers.Count);

		// This loop handles only powered down buildings, buildings, which generate power and remaining buildings, which do not cause low power state.
		for (var i = 0; i < this.powers.Count; i++)
		{
			var (actor, power) = this.powers.ElementAt(i);

			if (power.IsTraitDisabled)
			{
				power.SetPowered(actor, false);

				continue;
			}

			var consume = -power.Info.Amount;

			if (consume <= 0)
			{
				power.SetPowered(actor, true);
				powered++;

				continue;
			}

			remaining -= consume;

			if (remaining >= 0)
			{
				power.SetPowered(actor, true);
				powered++;
			}
			else
			{
				lowPowered.Add((actor, power));
			}
		}

		// Check, if there's enough power
		if (remaining >= 0)
		{
			this.currentLowPowerGroup = null;

			return;
		}

		// When in low power state, in each power cycle a group of buildings are powered
		// Size of the group is dependent on how big is the power deficit and how many power plants player has
		var maxLowerPowerGroupSize = 1;
		if (this.Power < 0 && powerPlantCount > 0)
		{
			// Calculate group size
			maxLowerPowerGroupSize =
				this.info.LowPowerInitialGroupSize    // start with the initial group size
				+ (
					remaining + 1   // +1 means that the thresholds are inclusive
				) / this.info.LowPowerThresholdDiff;  // divide with the threshold. This gives the number of buildings that can be powered

			// Each power plant adds one additional building to the group (up to a specified limit)
			maxLowerPowerGroupSize += Math.Min(this.info.LowPowerExtraGroupsLimit, powerPlantCount - 1);
			if (maxLowerPowerGroupSize < 1)
				maxLowerPowerGroupSize = 1;
		}

		// Give power to buildings, which are supposed to be online in current power cycle
		for (var i = 0; i < lowPowered.Count; i++)
		{
			var (actor, power) = lowPowered[i];
			this.currentLowPowerGroup ??= 0;

			var shouldGetPower = i / maxLowerPowerGroupSize == this.currentLowPowerGroup;

			power.SetPowered(actor, shouldGetPower);
		}

		if (self.World.WorldTick % this.info.PowerCycleInterval == 0 && this.currentLowPowerGroup != null)
		{
			// Calculate next group to power on in next power cycle.
			var div = Exts.IntegerDivisionRoundingAwayFromZero(lowPowered.Count, maxLowerPowerGroupSize);
			this.currentLowPowerGroup = (this.currentLowPowerGroup + 1) % Math.Max(2, div);
		}

		if (Game.RunTime <= this.lastAdviceTime + this.info.AdviceInterval)
			return;

		Game.Sound.PlayNotification(self.World.Map.Rules, self.Owner, "Speech", this.info.SpeechNotification, self.Owner.Faction.InternalName);
		TextNotificationsManager.AddTransientLine(self.Owner, this.info.TextNotification);

		this.lastAdviceTime = Game.RunTime;
	}
}
