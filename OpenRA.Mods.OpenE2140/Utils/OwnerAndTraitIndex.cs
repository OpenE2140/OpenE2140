using OpenRA.Mods.Common;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Utils;

public class OwnerAndTraitIndex<TTraitInfo> : ActorIndex
	where TTraitInfo : ITraitInfoInterface
{
	private readonly HashSet<string> names;
	private readonly Player owner;

	public OwnerAndTraitIndex(World world, IReadOnlyCollection<string> names, Player owner)
		: base(world, ActorsToIndex(world, names.ToHashSet(), owner))
	{
		this.names = names.ToHashSet();
		this.owner = owner;
	}

	private static IEnumerable<Actor> ActorsToIndex(World world, HashSet<string> names, Player owner)
	{
		return world.Actors.Where(a => ShouldIndexActor(a, names, owner));
	}

	protected override bool ShouldIndexActor(Actor actor)
	{
		return ShouldIndexActor(actor, this.names, this.owner);
	}

	private static bool ShouldIndexActor(Actor actor, HashSet<string> names, Player owner)
	{
		return actor.Owner == owner
			&& (names.Count == 0 || names.Contains(actor.Info.Name))
			&& actor.Info.HasTraitInfo<TTraitInfo>();
	}
}
