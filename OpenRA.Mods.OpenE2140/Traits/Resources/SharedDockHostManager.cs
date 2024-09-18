using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

public interface ISharedDockHostManagerInfo : ITraitInfoInterface { }

public interface ISharedDockHostManager
{
	bool IsDockingPossible(SharedDockHost sharedDockHost, Actor clientActor, IDockClient client, bool ignoreReservations = false);
}

public class SharedDockHostManager<InfoType> : PausableConditionalTrait<InfoType>, ISharedDockHostManager
	where InfoType : PausableConditionalTraitInfo
{
	public SharedDockHostManager(InfoType info)
		: base(info)
	{
	}

	public virtual bool IsDockingPossible(SharedDockHost sharedDockHost, Actor clientActor, IDockClient client, bool ignoreReservations = false)
	{
		return true;
	}
}
