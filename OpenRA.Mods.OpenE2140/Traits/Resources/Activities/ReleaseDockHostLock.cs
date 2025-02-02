using OpenRA.Activities;

namespace OpenRA.Mods.OpenE2140.Traits.Resources.Activities;

/// <summary>
/// Releases lock on <see cref="SharedDockHost"/> when the child activity finishes.
/// </summary>
public class ReleaseDockHostLock : Activity
{
	private readonly SharedDockHost sharedDockHost;
	private readonly Activity childActivity;

	public ReleaseDockHostLock(SharedDockHost sharedDockHost, Activity childActivity)
	{
		this.sharedDockHost = sharedDockHost;
		this.childActivity = childActivity;
	}

	protected override void OnFirstRun(Actor self)
	{
		this.QueueChild(this.childActivity);
	}

	protected override void OnLastRun(Actor self)
	{
		this.sharedDockHost.ReleaseLock(self);
	}

	protected override void OnActorDispose(Actor self)
	{
		this.OnLastRun(self);
	}
}
