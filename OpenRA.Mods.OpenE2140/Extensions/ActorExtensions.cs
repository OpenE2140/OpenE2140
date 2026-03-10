#region Copyright & License Information

/*
 * Copyright (c) The OpenE2140 Developers and Contributors
 * This file is part of OpenE2140, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */

#endregion

using System.Diagnostics.CodeAnalysis;

namespace OpenRA.Mods.OpenE2140.Extensions;

public static class ActorExtensions
{
	public static IEnumerable<T> GetWorldAndPlayersTraitsImplementing<T>(this World world)
	{
		if (world.Disposing)
			return [];

		return Iterate(world);

		static IEnumerable<T> Iterate(World world)
		{
			foreach (var trait in world.WorldActor.TraitsImplementing<T>())
				yield return trait;
			foreach (var trait in world.Players.SelectMany(p => p.PlayerActor.TraitsImplementing<T>()))
				yield return trait;
		}
	}

	public static IEnumerable<T> GetSelfAndOwnerTraitsImplementing<T>(this Actor actor)
	{
		if (actor.Disposed)
			return [];

		return Iterate(actor);

		static IEnumerable<T> Iterate(Actor actor)
		{
			foreach (var trait in actor.TraitsImplementing<T>())
				yield return trait;
			foreach (var trait in actor.Owner.PlayerActor.TraitsImplementing<T>())
				yield return trait;
		}
	}


	public static IEnumerable<T> TryGetTraitsImplementing<T>(this Actor actor)
	{
		return actor.IsInWorld ? actor.TraitsImplementing<T>() : [];
	}

	public static bool TryGetTrait<T>(this Actor actor, [MaybeNullWhen(false)] out T trait)
	{
		if (actor.Disposed)
		{
			trait = default;

			return false;
		}

		trait = actor.TraitOrDefault<T>();

		return trait != null;
	}

	public static T? GetTraitOrDefault<T>(this Actor? actor)
	{
		var traitOrDefault = actor is { IsInWorld: true } ? actor.TraitOrDefault<T>() : default;

		return traitOrDefault;
	}

	public static void TryGrantingCondition(this Actor actor, ref int token, string? condition)
	{
		if (token == Actor.InvalidConditionToken)
			token = actor.GrantCondition(condition);
	}

	public static void TryRevokingCondition(this Actor actor, ref int token)
	{
		if (token != Actor.InvalidConditionToken)
			token = actor.RevokeCondition(token);
	}

	public static void GrantOrRevokeCondition(this Actor actor, ref int token, bool isEnabled, string? condition)
	{
		if (isEnabled && token == Actor.InvalidConditionToken)
			token = actor.GrantCondition(condition);
		else if (!isEnabled && token != Actor.InvalidConditionToken)
			token = actor.RevokeCondition(token);
	}
}
