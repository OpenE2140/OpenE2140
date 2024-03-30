using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Miner;

public class BuildWallOrderGenerator : UnitOrderGenerator
{
	private TraitPair<WallBuilder>[] subjects;

	public BuildWallOrderGenerator(IEnumerable<Actor> subjects)
	{
		this.subjects = GetWallBuilders(subjects);
	}

	public override IEnumerable<Order> Order(OpenRA.World world, CPos cell, int2 worldPixel, MouseInput mi)
	{
		if (mi.Button == Game.Settings.Game.MouseButtonPreference.Cancel)
		{
			world.CancelInputMode();
			yield break;
		}

		if (mi.Button == Game.Settings.Game.MouseButtonPreference.Action)
		{
			var queued = mi.Modifiers.HasModifier(Modifiers.Shift);
			if (!queued)
			{
				world.CancelInputMode();
			}

			yield return new Order(
				WallBuilder.BuildWallOrderID,
				null,
				Target.FromCell(world, cell),
				queued,
				groupedActors: this.subjects.Select(p => p.Actor).ToArray());
		}
	}

	public override void SelectionChanged(OpenRA.World world, IEnumerable<Actor> selected)
	{
		this.subjects = GetWallBuilders(selected);

		if (!this.subjects.Any(s => s.Actor.Info.HasTraitInfo<AutoTargetInfo>()))
			world.CancelInputMode();
	}

	private static TraitPair<WallBuilder>[] GetWallBuilders(IEnumerable<Actor> actors)
	{
		return actors
			.Where(s => !s.IsDead)
			.SelectMany(a => a.TraitsImplementing<WallBuilder>()
				.Select(am => new TraitPair<WallBuilder>(a, am)))
			.ToArray();
	}

	public override string? GetCursor(OpenRA.World world, CPos cell, int2 worldPixel, MouseInput mi)
	{
		var target = TargetForInput(world, cell, worldPixel, mi);

		var subject = this.subjects.FirstOrDefault();
		if (subject.Actor == null)
		{
			return null;
		}

		var isValid = subject.Trait.IsCellAcceptable(subject.Actor, world.Map.CellContaining(target.CenterPosition));
		if (target.Actor != null)
		{
			isValid &= target.Actor == subject.Actor;
		}

		return isValid ? subject.Trait.Info.BuildCursor : subject.Trait.Info.BuildBlockedCursor;
	}

	public override bool InputOverridesSelection(OpenRA.World world, int2 xy, MouseInput mi)
	{
		return true;
	}
}
