using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Production;

[Desc("Hides actors, which prerequisite technologies can't be researched due to research limit restriction. Attach to all factory buildings.")]
public class HideUnbuildableActorsInfo : TraitInfo<HideUnbuildableActors>, Requires<ProductionQueueInfo> { }

public class HideUnbuildableActors : INotifyAddedToWorld, INotifyOwnerChanged
{
	void INotifyAddedToWorld.AddedToWorld(Actor self)
	{
		var research = self.Owner.PlayerActor.Trait<Research.Research>();

		research.HideUnbuildableActors(self);
	}

	void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
	{
		var research = newOwner.PlayerActor.Trait<Research.Research>();

		research.HideUnbuildableActors(self);
	}
}
