using JetBrains.Annotations;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Power;

[TraitLocation(SystemActors.Player)]
[Desc("Earth specific variant of the PowerManager trait. This is the modern version, with improved behavior.")]
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class ModernPowerManagerInfo : PowerManagerBaseInfo
{
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

	public override object Create(ActorInitializer init)
	{
		return new ModernPowerManager(init.Self, this);
	}
}

public class ModernPowerManager : PowerManagerBase
{
	private readonly ModernPowerManagerInfo info;
	private int? currentLowPowerGroup;

	public ModernPowerManager(Actor self, ModernPowerManagerInfo info)
		: base(self, info)
	{
		this.info = info;
	}

	protected override PlayerPowerState TickInner(Actor self)
	{
		var remaining = this.PowerGenerated;

		if (this.DevMode.UnlimitedPower)
			remaining = int.MaxValue;
		else if (this.Power < 0)
			remaining = remaining * this.info.UsableEnergyWhenLowPowerPercent / 100;

		var powerPlantCount = this.Powers.Values.Count(p => !p.IsTraitDisabled && p.Info.Amount > 0);

		var powered = 0;
		var lowPowered = new List<(Actor, Power)>(this.Powers.Count);

		// This loop handles only powered down buildings, buildings, which generate power and remaining buildings, which do not cause low power state.
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

			return PlayerPowerState.Ok;
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

		return PlayerPowerState.LowPower;
	}
}
