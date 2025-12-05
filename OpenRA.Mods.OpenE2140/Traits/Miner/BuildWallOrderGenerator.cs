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

using OpenRA.Graphics;
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits;

public class BuildWallOrderGenerator : UnitOrderGenerator
{
	private TraitPair<WallBuilder>[] subjects;

	public BuildWallOrderGenerator(IEnumerable<Actor> subjects)
	{
		this.subjects = GetWallBuilders(subjects);
	}

	public override IEnumerable<Order> Order(World world, CPos cell, int2 worldPixel, MouseInput mi)
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

			if (mi.Modifiers.HasModifier(Modifiers.Ctrl))
				world.OrderGenerator = new LineBuildOrderGenerator(cell, this.subjects, queued);
			else
				yield return new Order(
					WallBuilder.BuildWallOrderID,
					null,
					Target.FromCell(world, cell),
					queued,
					groupedActors: this.subjects.Select(p => p.Actor).ToArray());
		}
	}

	public override void SelectionChanged(World world, IEnumerable<Actor> selected)
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

	public override string? GetCursor(World world, CPos cell, int2 worldPixel, MouseInput mi)
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

	public override bool InputOverridesSelection(World world, int2 xy, MouseInput mi)
	{
		return true;
	}

	private class LineBuildOrderGenerator : UnitOrderGenerator
	{
		private readonly CPos startPosition;
		private readonly TraitPair<WallBuilder>[] wallBuilders;
		private readonly Sprite validTile, unknownTile, blockedTile;
		private readonly float validAlpha, unknownAlpha, blockedAlpha;
		private readonly bool queued;

		public LineBuildOrderGenerator(CPos startPosition, TraitPair<WallBuilder>[] wallBuilders, bool queued)
		{
			this.startPosition = startPosition;
			this.wallBuilders = wallBuilders;

			this.queued = queued;

			var a = wallBuilders[0].Actor;
			var wallBuilder = wallBuilders[0].Trait;
			var tileset = a.World.Map.Tileset.ToLowerInvariant();
			var sequences = a.World.Map.Sequences;
			if (sequences.HasSequence("overlay", $"{wallBuilder.Info.TileValidName}-{tileset}"))
			{
				var validSequence = sequences.GetSequence("overlay", $"{wallBuilder.Info.TileValidName}-{tileset}");
				this.validTile = validSequence.GetSprite(0);
				this.validAlpha = validSequence.GetAlpha(0);
			}
			else
			{
				var validSequence = sequences.GetSequence("overlay", wallBuilder.Info.TileValidName);
				this.validTile = validSequence.GetSprite(0);
				this.validAlpha = validSequence.GetAlpha(0);
			}

			if (sequences.HasSequence("overlay", $"{wallBuilder.Info.TileUnknownName}-{tileset}"))
			{
				var unknownSequence = sequences.GetSequence("overlay", $"{wallBuilder.Info.TileUnknownName}-{tileset}");
				this.unknownTile = unknownSequence.GetSprite(0);
				this.unknownAlpha = unknownSequence.GetAlpha(0);
			}
			else
			{
				var unknownSequence = sequences.GetSequence("overlay", wallBuilder.Info.TileUnknownName);
				this.unknownTile = unknownSequence.GetSprite(0);
				this.unknownAlpha = unknownSequence.GetAlpha(0);
			}

			if (sequences.HasSequence("overlay", $"{wallBuilder.Info.TileInvalidName}-{tileset}"))
			{
				var blockedSequence = sequences.GetSequence("overlay", $"{wallBuilder.Info.TileInvalidName}-{tileset}");
				this.blockedTile = blockedSequence.GetSprite(0);
				this.blockedAlpha = blockedSequence.GetAlpha(0);
			}
			else
			{
				var blockedSequence = sequences.GetSequence("overlay", wallBuilder.Info.TileInvalidName);
				this.blockedTile = blockedSequence.GetSprite(0);
				this.blockedAlpha = blockedSequence.GetAlpha(0);
			}
		}

		public override IEnumerable<Order> Order(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			if (mi.Button == Game.Settings.Game.MouseButtonPreference.Cancel)
			{
				world.CancelInputMode();
				yield break;
			}

			if (mi.Button == Game.Settings.Game.MouseButtonPreference.Action)
			{
				var queued = mi.Modifiers.HasModifier(Modifiers.Shift) || this.queued;
				world.CancelInputMode();

				yield return new Order(
					WallBuilder.BuildWallLineOrderID,
					null,
					Target.FromCell(world, cell),
					queued,
					groupedActors: this.wallBuilders.Select(p => p.Actor).ToArray())
				{
					ExtraLocation = this.startPosition
				};
			}
		}

		public override IEnumerable<IRenderable> RenderAboveShroud(WorldRenderer wr, World world)
		{
			var lastMousePos = wr.Viewport.ViewToWorld(Viewport.LastMousePos);

			var validEndPosition = this.wallBuilders
				.Select(p => p.Trait.GetNearestValidLineBuildPosition(p.Actor, this.startPosition, lastMousePos))
				.FirstOrDefault();

			var builder = this.wallBuilders[0];
			var wallLineCells = builder.Trait.GetLineBuildCells(builder.Actor, this.startPosition, lastMousePos);

			var movement = builder.Actor.Trait<IPositionable>();
			var mobile = movement as Mobile;
			foreach (var cell in wallLineCells)
			{
				var tile = this.validTile;
				var alpha = this.validAlpha;
				if (!world.Map.Contains(cell))
				{
					tile = this.blockedTile;
					alpha = this.blockedAlpha;
				}
				else if (world.ShroudObscures(cell))
				{
					tile = this.blockedTile;
					alpha = this.blockedAlpha;
				}
				else if (world.FogObscures(cell))
				{
					tile = this.unknownTile;
					alpha = this.unknownAlpha;
				}
				else if (!builder.Trait.IsCellAcceptable(builder.Actor, cell)
					|| !movement.CanEnterCell(cell, null, BlockedByActor.Immovable) || (mobile != null && !mobile.CanStayInCell(cell)))
				{
					tile = this.blockedTile;
					alpha = this.blockedAlpha;
				}

				yield return new SpriteRenderable(tile, world.Map.CenterOfCell(cell), WVec.Zero, -511, null, 1f, alpha, float3.Ones, TintModifiers.IgnoreWorldTint, true);
			}
		}

		public override string? GetCursor(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			var target = TargetForInput(world, cell, worldPixel, mi);

			var subject = this.wallBuilders.FirstOrDefault();
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

		public override void SelectionChanged(World world, IEnumerable<Actor> selected)
		{
			world.CancelInputMode();
		}
	}
}
