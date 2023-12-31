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

using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.WaterBase;

public static class WaterBaseUtils
{
	public static void TogglePoweredDownState(Actor self)
	{
		var traits = self.TraitsImplementing<ToggleConditionOnOrder>()
			.Where(o => o.Info.OrderName == "PowerDown")
			.Cast<IResolveOrder>();

		foreach (var trait in traits)
		{
			trait.ResolveOrder(self, new Order("PowerDown", self, false));
		}
	}

	public static ActorInfo FindWaterBaseMcuActor(Ruleset rules)
	{
		return rules.Actors.Values.First(a => a.HasTraitInfo<WaterBaseTransformsInfo>());
	}

	public static ActorInfo FindWaterBaseBuildingActor(Ruleset rules)
	{
		return rules.Actors[FindWaterBaseMcuActor(rules).TraitInfo<WaterBaseTransformsInfo>().IntoActor];
	}

	public static WaterBaseTransformsInfo? FindWaterBaseMcuTransformsFromBuildingActor(Ruleset rules, ActorInfo buildingActor)
	{
		return rules.Actors.Values.FirstOrDefault(a => a.TraitInfoOrDefault<WaterBaseTransformsInfo>()?.IntoActor == buildingActor.Name)
			?.TraitInfoOrDefault<WaterBaseTransformsInfo>();
	}

	public static ActorInfo FindWaterBaseDockActor(Ruleset rules)
	{
		return rules.Actors.Values.First(a => a.HasTraitInfo<WaterBaseDockInfo>());
	}
}
