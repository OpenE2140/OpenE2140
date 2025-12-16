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

	private bool hasDocked;

	public TransportCrates(Actor self)
	{
		this.crateTransporter = self.Trait<CrateTransporter>();
		this.routine = self.Trait<CrateTransporterRoutine>();
		this.dockClient = self.Trait<DockClientManager>();

		this.ChildHasPriority = false;
	}

	protected override void OnFirstRun(Actor self)
	{
		if (this.routine.Info.AssignTargetsAutomatically || this.GetDockHostActor() != null)
			this.QueueChild(new MoveToDock(self, dockHostActor: this.GetDockHostActor(), dockLineColor: this.dockClient.DockLineColor));
	}

	private Actor? GetDockHostActor()
	{
		this.routine.UpdateTargets();
		return this.crateTransporter.HasCrate ? this.routine.CurrentRefinery : this.routine.CurrentMine;
	}

	public override bool Tick(Actor self)
	{
		if (this.IsCanceling || !this.routine.Info.AssignTargetsAutomatically)
			return this.TickChild(self);

		if (this.crateTransporter.DockingInProgress == true)
			this.hasDocked = true;

		if (!this.TickChild(self))
			return false;

		// If MoveToDock cancels itself, it means it could find any available dock and gave up.
		// However we can't tell this just from looking at the activity: the activity state transitions to Done before TickChild() returns.
		if (this.hasDocked)
			return true;

		// The transport routine continues after waiting a bit.
		this.QueueChild(new Wait(this.dockClient.Info.SearchForDockDelay));
		this.QueueChild(new MoveToDock(self, dockHostActor: this.GetDockHostActor(), dockLineColor: this.dockClient.DockLineColor));

		return false;
	}

	public override IEnumerable<TargetLineNode> TargetLineNodes(Actor self)
	{
		return this.ChildActivity?.TargetLineNodes(self) ?? [];
	}
}
