namespace OpenRA.Mods.OpenE2140.Extensions;

public static class TraitPairExtensions
{
	public static void Deconstruct<T>(this TraitPair<T> traitPair, out Actor actor, out T trait)
	{
		actor = traitPair.Actor;
		trait = traitPair.Trait;
	}
}
