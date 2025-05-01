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
