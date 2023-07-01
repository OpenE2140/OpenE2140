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

using JetBrains.Annotations;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Effects;
using OpenRA.Mods.OpenE2140.Helpers;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.World;

[TraitLocation(SystemActors.World)]
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("Render animation on specific terrain tiles. Attach to World actor.")]
public class TerrainTileAnimationInfo : TraitInfo, IRulesetLoaded, ILobbyCustomRulesIgnore
{
	[FieldLoader.Require]
	[Desc("Tiles to play animation on (Id field of terrain template).")]
	public readonly ushort[] Tiles = Array.Empty<ushort>();

	[Desc("Average time (ticks) between animations.")]
	public readonly int[] Interval = { 7 * 25, 13 * 25 };

	[Desc($"Delays (in ticks) before each loop. Number of delays must be equal or less than {nameof(TerrainTileAnimationInfo.LoopCount)}.")]
	public readonly int[] LoopDelays = { 0 };

	[Desc("Number of loops.")]
	public readonly int LoopCount = 1;

	[Desc("Offset of the animation.")]
	public readonly WVec Offset = WVec.Zero;

	[FieldLoader.Require]
	[Desc("Image to render.")]
	public readonly string Image = "";

	[Desc("List of sequences to render for each loop. Each sequence corresponds to loop.")]
	[SequenceReference(nameof(TerrainTileAnimationInfo.Image))]
	public readonly string[] Sequences = { "idle" };

	[FieldLoader.Require]
	[Desc("Which palette to use.")]
	[PaletteReference]
	public readonly string Palette = "effect";

	[Desc(
		$"Change effect position each tick. List of triples (x,y,z) for each loop. ",
		$"Number of items must be equal or less than {nameof(TerrainTileAnimationInfo.LoopCount)}. By default effect does not move."
	)]
	public readonly WVec[] EffectMovement = Array.Empty<WVec>();

	public override object Create(ActorInitializer init) { return new TerrainTileAnimation(init.Self, this); }

	public void RulesetLoaded(Ruleset rules, ActorInfo info)
	{
		if (this.LoopDelays.Length > this.LoopCount)
		{
			throw new YamlException(
				$"{nameof(TerrainTileAnimation)} has incorrect number of {nameof(this.LoopDelays)} configured. "
				+ $"Must be equal or lower than {nameof(this.LoopCount)} ({this.LoopCount})."
			);
		}

		if (this.EffectMovement.Length > this.LoopCount)
		{
			throw new YamlException(
				$"{nameof(TerrainTileAnimation)} has incorrect number of {nameof(this.EffectMovement)} configured. "
				+ $"Must be equal or lower than {nameof(this.LoopCount)} ({this.LoopCount})."
			);
		}

		if (this.Sequences.Length != this.LoopCount)
		{
			throw new YamlException(
				$"{nameof(TerrainTileAnimation)} has incorrect number of {nameof(this.Sequences)} configured. "
				+ $"Must be equal to {nameof(this.LoopCount)} ({this.LoopCount})."
			);
		}
	}
}

public class TerrainTileAnimation : ITick
{
	private readonly TerrainTileAnimationInfo info;
	private readonly CPos[] cells;
	private readonly Dictionary<CPos, TileAnimation> tileAnimations = new Dictionary<CPos, TileAnimation>();
	private readonly List<CPos> tileAnimationsCleanup = new List<CPos>();

	private int ticks;

	public TerrainTileAnimation(Actor self, TerrainTileAnimationInfo info)
	{
		this.info = info;

		var map = self.World.Map;
		this.cells = map.AllCells.Where(cell => info.Tiles.Contains(map.Tiles[cell].Type)).ToArray();
		this.tileAnimationsCleanup.Capacity = this.cells.Length;
	}

	void ITick.Tick(Actor self)
	{
		if (!this.cells.Any())
			return;

		var world = self.World;

		foreach (var (cell, anim) in this.tileAnimations)
		{
			anim.Update(world);

			if (!anim.IsActive)
				this.tileAnimationsCleanup.Add(cell);
		}

		foreach (var cell in this.tileAnimationsCleanup)
			this.tileAnimations.Remove(cell);

		this.tileAnimationsCleanup.Clear();

		if (--this.ticks <= 0)
		{
			this.ticks = Util.RandomInRange(world.LocalRandom, this.info.Interval);
			var cell = this.cells.Random(world.LocalRandom);

			if (this.tileAnimations.ContainsKey(cell))
				return;

			var position = world.Map.CenterOfCell(cell) + this.info.Offset;

			var anim = new TileAnimation(this.info, position);
			this.tileAnimations.Add(cell, anim);
			anim.Update(world);
		}
	}

	private class TileAnimation
	{
		private readonly TerrainTileAnimationInfo info;
		private readonly WPos initialPosition;
		private SpriteEffect? effect = null;
		private int delayTicks;
		private int movementTicks;
		private int loop;

		public bool IsActive => this.loop < this.info.LoopCount;

		public TileAnimation(TerrainTileAnimationInfo info, WPos initialPosition)
		{
			this.info = info;
			this.initialPosition = initialPosition;
			this.delayTicks = info.LoopDelays.GetElementOrDefault(0);
		}

		public void Update(OpenRA.World world)
		{
			if (this.effect == null && this.delayTicks-- <= 0)
			{
				WPos Position()
				{
					return this.initialPosition + this.info.EffectMovement.GetElementOrDefault(this.loop, WVec.Zero) * this.movementTicks++;
				}

				this.effect = new SpriteEffect(Position, () => WAngle.Zero, world, this.info.Image, this.info.Sequences[this.loop], this.info.Palette);
				world.AddFrameEndTask(w => w.Add(this.effect));
			}
			else if (this.effect != null && !world.Effects.Contains(this.effect))
			{
				this.loop++;
				this.delayTicks = this.info.LoopDelays.GetElementOrDefault(this.loop);
				this.movementTicks += this.delayTicks - 1;
				this.effect = null;
			}
		}
	}
}
