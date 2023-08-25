namespace OpenRA.Mods.OpenE2140.Warheads;

using Common;
using Common.Traits;
using Common.Warheads;
using OpenRA.Traits;
using Primitives;

public abstract class EffectWarhead : Warhead
{
	[Desc("Whether to consider actors in determining whether the explosion should happen. If false, only terrain will be considered.")]
	public readonly bool ImpactActors = true;

	protected static readonly BitSet<TargetableType> TargetTypeAir = new("Air");

	/// <summary>
	/// Checks if target is a valid type of actor.
	/// </summary>
	/// <param name="victim"></param>
	/// <param name="firedBy"></param>
	/// <returns></returns>
	public override bool IsValidAgainst(Actor victim, Actor firedBy)
	{
		var relationship = firedBy.Owner.RelationshipWith(victim.Owner);

		// A target type is valid if it is in the valid targets list, and not in the invalid targets list.
		return this.ValidRelationships.HasRelationship(relationship) && this.IsValidTarget(victim.GetEnabledTargetTypes());
	}

	/// <summary>
	/// Checks if target is a valid type of terrain.
	/// </summary>
	/// <param name="map">Map assigned to the <see cref="World"/>.</param>
	/// <param name="pos">Position on the map.</param>
	/// <returns></returns>
	protected bool IsValidAgainst(Map map, WPos pos)
	{
		var cell = map.CellContaining(pos);

		if (!map.Contains(cell))
			return false;

		var dat = map.DistanceAboveTerrain(pos);

		return this.IsValidTarget(dat > this.AirThreshold ? EffectWarhead.TargetTypeAir : map.GetTerrainInfo(cell).TargetTypes);
	}

	/// <summary>
	/// Checks if target is a valid type.
	/// </summary>
	/// <param name="target"></param>
	/// <param name="firedBy"></param>
	/// <returns></returns>
	protected bool IsValidAgainst(in Target target, Actor firedBy)
	{
		if (target.Type == TargetType.Invalid)
			return false;

		var pos = new WPos(target.CenterPosition.X, target.CenterPosition.Y, Math.Max(0, target.CenterPosition.Z));
		var world = firedBy.World;

		var actorTypeAtImpact = this.ImpactActors ? this.GetActorTypeAtImpact(world, pos, firedBy) : ImpactActorType.None;

		switch (actorTypeAtImpact)
		{
			// Ignore the impact if there are only invalid actors within range
			case ImpactActorType.Invalid:
			// Ignore the impact if there are no valid actors and no valid terrain
			// (impacts are allowed on valid actors sitting on invalid terrain!)
			case ImpactActorType.None when !this.IsValidAgainst(world.Map, pos):
				return false;

			default:
				return true;
		}
	}

	protected ImpactActorType GetActorTypeAtImpact(World world, WPos pos, Actor firedBy)
	{
		// Check whether the impact position overlaps with an actor's hitshape
		var victims = world.FindActorsOnCircle(pos, WDist.Zero)
			.Where(actor => actor != firedBy)
			.Select(actor => new { Actor = actor, HitShapes = actor.TraitsImplementing<HitShape>().Where(shape => !shape.IsTraitDisabled) })
			.Where(victim => victim.HitShapes.Any(shape => shape.DistanceFromEdge(victim.Actor, pos).Length <= 0))
			.Select(arg => arg.Actor)
			.ToList();

		var impactedActorsFound = victims.Any();
		var validTargetsFound = victims.Any(victim => this.IsValidAgainst(victim, firedBy));

		return impactedActorsFound ? validTargetsFound ? ImpactActorType.Valid : ImpactActorType.Invalid : ImpactActorType.None;
	}
}
