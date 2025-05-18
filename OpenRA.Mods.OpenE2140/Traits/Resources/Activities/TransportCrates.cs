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

using OpenRA.Activities;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Resources.Activities;

public class TransportCrates : Activity
{
	private readonly CrateTransporter crateTransporter;
	private readonly CrateTransporterRoutine routine;
	private readonly DockClientManager dockClient;

	public TransportCrates(Actor self)
	{
		this.crateTransporter = self.Trait<CrateTransporter>();
		this.routine = self.Trait<CrateTransporterRoutine>();
		this.dockClient = self.Trait<DockClientManager>();
	}

	protected override void OnFirstRun(Actor self)
	{
		Actor? dockHostActor;
		if (this.crateTransporter.HasCrate)
		{
			dockHostActor = this.routine.CurrentRefinery;
		}
		else
		{
			dockHostActor = this.routine.CurrentMine;
		}

		if (this.routine.Info.AssignTargetsAutomatically || dockHostActor != null)
			this.QueueChild(new MoveToDock(self, dockHostActor: dockHostActor, dockLineColor: this.dockClient.DockLineColor));
	}

	public override IEnumerable<TargetLineNode> TargetLineNodes(Actor self)
	{
		return this.ChildActivity?.TargetLineNodes(self) ?? [];
	}
}
