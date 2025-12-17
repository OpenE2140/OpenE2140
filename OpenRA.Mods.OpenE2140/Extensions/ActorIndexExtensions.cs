using OpenRA.Mods.Common;

namespace OpenRA.Mods.OpenE2140.Extensions;

public static class ActorIndexExtensions
{
	public static IEnumerable<Actor> Alive(this ActorIndex index)
	{
		return index.Actors.Where(a => !a.IsDead);
	}
}
