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

namespace OpenRA.Mods.OpenE2140.Traits.BotModules;

[TraitLocation(SystemActors.Player)]
[Desc("Manages AI repairing base buildings, which just completed transformation from MCU.")]
public class DeployedMcuBuildingRepairBotModuleInfo : ConditionalTraitInfo
{
	public override object Create(ActorInitializer init) { return new DeployedMcuBuildingRepairBotModule(this); }
}

public class DeployedMcuBuildingRepairBotModule : ConditionalTrait<DeployedMcuBuildingRepairBotModuleInfo>, IBotMcuDeployment
{
	private IBotRequestBuildingRepair? botRequestBuildRepair;

	public DeployedMcuBuildingRepairBotModule(DeployedMcuBuildingRepairBotModuleInfo info)
		: base(info)
	{
	}

	protected override void Created(Actor self)
	{
		base.Created(self);

		this.botRequestBuildRepair = self.Owner.PlayerActor.TraitOrDefault<IBotRequestBuildingRepair>();
	}

	void IBotMcuDeployment.McuTransformed(IBot bot, Actor buildingActor)
	{
		if (this.IsTraitDisabled || buildingActor.GetDamageState() == DamageState.Undamaged)
			return;

		this.botRequestBuildRepair?.RequestBuildingRepair(bot, buildingActor);
	}
}
