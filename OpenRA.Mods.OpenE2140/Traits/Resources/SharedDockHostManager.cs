using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

public interface ISharedDockHostManagerInfo : ITraitInfoInterface { }

public interface ISharedDockHostManager
{
	bool IsDockingPossible(SharedDockHost sharedDockHost, Actor clientActor, IDockClient client, bool ignoreReservations = false);

	bool TryAcquireLock(Actor clientActor);

	void TryReleaseLock(Actor clientActor);
}

public class SharedDockHostManager<InfoType> : PausableConditionalTrait<InfoType>, ISharedDockHostManager
	where InfoType : PausableConditionalTraitInfo
{
	public Actor? CurrentClientActor { get; private set; }

	public SharedDockHostManager(InfoType info)
		: base(info)
	{
	}

	public virtual bool IsDockingPossible(SharedDockHost sharedDockHost, Actor clientActor, IDockClient client, bool ignoreReservations = false)
	{
		return true;
	}

	public bool TryAcquireLock(Actor clientActor)
	{
		if (this.CurrentClientActor == clientActor)
			return true;

		if (!this.TryAcquireLockInner(clientActor))
			return false;

		if (this.CurrentClientActor == null)
		{
			this.CurrentClientActor = clientActor;

			return true;
		}

		return false;
	}

	protected virtual bool TryAcquireLockInner(Actor clientActor)
	{
		return true;
	}

	public void TryReleaseLock(Actor clientActor)
	{
		if (this.CurrentClientActor == clientActor)
			this.CurrentClientActor = null;
	}
}
