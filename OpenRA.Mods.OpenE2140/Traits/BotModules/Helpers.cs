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

using OpenRA.Mods.OpenE2140.Traits.Power;

namespace OpenRA.Mods.OpenE2140.Traits.BotModules;

public static class Helpers
{
	public static bool HasSufficientPowerForBuilding(PowerManagerBase? powerManager, ActorInfo actorInfo, int minimumExcessPower)
	{
		return powerManager == null || actorInfo.TraitInfos<PowerInfo>()
			.Where(i => i.EnabledByDefault)
			.Sum(p => p.Amount) + powerManager.Power >= minimumExcessPower;
	}
}
