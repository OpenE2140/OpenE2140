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

namespace OpenRA.Mods.OpenE2140.Traits.Misc;

public class NotifyRepairCallbackInfo : TraitInfo, Requires<RepairableBuildingInfo>
{
	public override object Create(ActorInitializer init)
	{
		return new NotifyRepairCallback(init.Self);
	}
}

public class NotifyRepairCallback : ITick, INotifyCreated
{
	private readonly RepairableBuilding repairBuilding;
	private List<INotifyBuildingRepair>? notifiers;
	private bool wasRepairing = false;

	public NotifyRepairCallback(Actor self)
	{
		this.repairBuilding = self.Trait<RepairableBuilding>();
	}

	void INotifyCreated.Created(Actor self)
	{
		this.notifiers = self.TraitsImplementing<INotifyBuildingRepair>().ToList();
	}

	public bool IsRepairing(Actor self)
	{
		return this.repairBuilding.RepairActive && self.GetDamageState() != DamageState.Undamaged;
	}

	void ITick.Tick(Actor self)
	{
		var isRepairing = this.IsRepairing(self);
		if (this.wasRepairing == isRepairing)
		{
			return;
		}

		if (isRepairing)
			this.notifiers?.ForEach(r => r.RepairStarted(self));
		else
			this.notifiers?.ForEach(r => r.RepairInterrupted(self));

		this.wasRepairing = isRepairing;
	}
}
