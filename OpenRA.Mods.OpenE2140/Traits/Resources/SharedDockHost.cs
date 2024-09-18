using OpenRA.Activities;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Traits.Resources.Activities;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

[Desc("A version of dock that shares docking position with other SharedDockHosts.")]
public class SharedDockHostInfo : DockHostInfo, IDockHostInfo, Requires<ISharedDockHostManagerInfo>
{
	[Desc("Docking type group.")]
	public readonly BitSet<DockType> GroupType;

	public override object Create(ActorInitializer init)
	{
		return new SharedDockHost(init.Self, this);
	}
}

public class SharedDockHost : DockHost
{
	public new readonly SharedDockHostInfo Info;

	private readonly ISharedDockHostManager manager;

	private SharedDockHost[] otherDockHosts = Array.Empty<SharedDockHost>();
	private Actor? currentClientActor;

	public SharedDockHost(Actor self, SharedDockHostInfo info)
		: base(self, info)
	{
		this.Info = info;
		this.manager = self.Trait<ISharedDockHostManager>();
	}

	protected override void Created(Actor self)
	{
		this.otherDockHosts = self.TraitsImplementing<SharedDockHost>()
			.Where(h => this.Info.GroupType.Overlaps(h.Info.GroupType) && h != this)
			.ToArray();
	}

	public override void QueueDockActivity(Activity moveToDockActivity, Actor self, Actor clientActor, DockClientManager client)
	{
		moveToDockActivity.QueueChild(new DockHostLock(this,
			new GenericDockSequence(
				clientActor,
				client,
				self,
				this,
				this.Info.DockWait,
				this.Info.IsDragRequired,
				this.Info.DragOffset,
				this.Info.DragLength)));
	}

	public override bool IsDockingPossible(Actor clientActor, IDockClient client, bool ignoreReservations = false)
	{
		if (!this.manager.IsDockingPossible(this, clientActor, client, ignoreReservations))
			return false;

		return base.IsDockingPossible(clientActor, client, ignoreReservations);
	}

	internal bool TryAcquireLock(Actor clientActor)
	{
		if ((this.currentClientActor == null)
			&& this.otherDockHosts.All(h => h.currentClientActor == null))
		{
			this.currentClientActor = clientActor;

			Array.ForEach(this.otherDockHosts, h => h.currentClientActor = clientActor);

			return true;
		}

		return false;
	}

	internal void ReleaseLock(Actor clientActor)
	{
		if (this.currentClientActor == clientActor)
		{
			this.currentClientActor = null;
			Array.ForEach(this.otherDockHosts, h => h.currentClientActor = null);
		}
	}
}
