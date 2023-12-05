using System.Diagnostics.CodeAnalysis;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Extensions;

public static class ActorInfoExtensions
{
	public static bool TryGetTrait<T>(this ActorInfo actorInfo, [MaybeNullWhen(false)] out T trait)
		where T : ITraitInfoInterface
	{
		trait = actorInfo.TraitInfoOrDefault<T>();

		return trait != null;
	}
}
