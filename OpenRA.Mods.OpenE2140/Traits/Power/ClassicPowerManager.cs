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
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits;

[TraitLocation(SystemActors.Player)]
[Desc("Earth specific variant of the PowerManager trait. This is the classic version, with the same logic as in E2140.")]
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class ClassicPowerManagerInfo : PowerManagerBaseInfo, IRulesetLoaded
{
	[Desc("Interval (in ticks) at which to to cycle the power actors on low power.")]
	public readonly int PowerCycleInterval = 9;

	[Desc($"Interval (in ticks) during which actors in low power state are getting power. Has to be lower than {nameof(PowerCycleInterval)}, but higher than 0")]
	public readonly int PowerOnInterval = 1;

	public override object Create(ActorInitializer init)
	{
		return new ClassicPowerManager(init.Self, this);
	}

	void IRulesetLoaded<ActorInfo>.RulesetLoaded(Ruleset rules, ActorInfo info)
	{
		if (this.PowerOnInterval >= this.PowerCycleInterval || this.PowerOnInterval < 0)
			throw new YamlException($"{nameof(this.PowerOnInterval)} has to be higher than 0 and lower than {nameof(this.PowerCycleInterval)}.");
	}
}

public class ClassicPowerManager : PowerManagerBase
{
	private readonly ClassicPowerManagerInfo info;
	private bool powerOn = true;

	public ClassicPowerManager(Actor self, ClassicPowerManagerInfo info)
		: base(self, info)
	{
		this.info = info;
	}

	protected override PlayerPowerState TickInner(Actor self)
	{
		var remaining = this.PowerGenerated;

		if (this.DevMode.UnlimitedPower)
			remaining = int.MaxValue;

		for (var i = 0; i < this.Powers.Count; i++)
		{
			var (actor, power) = this.Powers.ElementAt(i);

			if (power.IsTraitDisabled)
			{
				power.SetPowered(actor, false);

				continue;
			}

			var consume = -power.Info.Amount;

			if (consume <= 0)
			{
				power.SetPowered(actor, true);

				continue;
			}

			remaining -= consume;

			if (remaining >= 0)
				power.SetPowered(actor, true);
			else
				power.SetPowered(actor, this.powerOn);
		}

		// Check, if there's enough power
		if (remaining >= 0)
		{
			return PlayerPowerState.Ok;
		}

		if (self.World.WorldTick % this.info.PowerCycleInterval == 0)
		{
			this.powerOn = true;
		}
		else if (self.World.WorldTick % this.info.PowerCycleInterval == 0)
		{
			this.powerOn = false;
		}

		return PlayerPowerState.LowPower;
	}
}
