using OpenRA.Activities;
using OpenRA.Mods.Common.Activities;

namespace OpenRA.Mods.OpenE2140.Traits.Resources.Activities;

public class DockHostLock : Activity
{
	private readonly SharedDockHost sharedDockHost;
	private readonly Activity dockActivity;
	private readonly bool releaseOnFinish;

	private bool hasDockStarted;
	private bool wasCanceled;

	public DockHostLock(SharedDockHost sharedDockHost, Activity dockActivity, bool releaseOnFinish = true)
	{
		this.sharedDockHost = sharedDockHost;
		this.dockActivity = dockActivity;
		this.releaseOnFinish = releaseOnFinish;
		this.ChildHasPriority = false;
	}

	public override bool Tick(Actor self)
	{
		if (this.IsCanceling)
		{
			// Let child activities properly finish (undock, drag out, etc.)
			if (!this.TickChild(self))
				return false;

			this.wasCanceled = true;

			return true;
		}

		if (!this.TickChild(self))
			return false;

		if (!this.hasDockStarted)
		{
			if (!this.sharedDockHost.TryAcquireLock(self))
			{
				this.QueueChild(new Wait(5));
				return false;
			}

			this.hasDockStarted = true;
			this.QueueChild(this.dockActivity);
			return false;
		}

		return true;
	}

	protected override void OnLastRun(Actor self)
	{
		if (this.hasDockStarted && (this.releaseOnFinish || this.wasCanceled))
			this.sharedDockHost.ReleaseLock(self);
	}

	protected override void OnActorDispose(Actor self)
	{
		// CrateTransporter was destroyed, immediately release the lock (regardless of whether it should be held or not)
		this.sharedDockHost.ReleaseLock(self);
	}
}
