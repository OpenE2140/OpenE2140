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

using OpenRA.Mods.Common.Effects;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits;

public enum TrailType { Cell, CenterPosition }

[Desc("Renders a sprite effect when leaving a cell.")]
public class LeavesTrackTrailsInfo : ConditionalTraitInfo, Requires<BodyOrientationInfo>
{
	[FieldLoader.Require]
	public readonly string? Image;

	[SequenceReference(nameof(Image))]
	public readonly string[] Sequences = ["idle"];

	[PaletteReference]
	public readonly string Palette = "effect";

	[Desc("Only leave trail on listed terrain types. Leave empty to leave trail on all terrain types.")]
	public readonly HashSet<string> TerrainTypes = [];

	[Desc("Accepts values: Cell to draw the trail sprite in the center of the current cell,",
		"CenterPosition to draw the trail sprite at the current position.")]
	public readonly TrailType Type = TrailType.Cell;

	[Desc("Should the trail be visible through fog.")]
	public readonly bool VisibleThroughFog = false;

	[Desc("Display a trail while stationary.")]
	public readonly bool TrailWhileStationary = false;

	[Desc("Delay between trail updates when stationary.")]
	public readonly int StationaryInterval = 0;

	[Desc("Display a trail while moving.")]
	public readonly bool TrailWhileMoving = true;

	[Desc("Instantly change facing.")]
	public readonly bool ChangeFacingInstantly = false;

	[Desc("Delay between trail updates when moving.")]
	public readonly int MovingInterval = 0;

	[Desc("Delay before first trail.",
		"Use negative values for falling back to the *Interval values.")]
	public readonly int StartDelay = 0;

	[Desc("Trail spawn positions relative to actor position. (forward, right, up) triples")]
	public readonly WVec[] Offsets = [WVec.Zero];

	[Desc("Should the trail spawn relative to last position or current position?")]
	public readonly bool SpawnAtLastPosition = true;

	public override object Create(ActorInitializer init) { return new LeavesTrackTrails(this, init.Self); }
}

public class LeavesTrackTrails : ConditionalTrait<LeavesTrackTrailsInfo>, ITick
{
	private readonly BodyOrientation body;
	private IFacing? facing;
	private WAngle cachedFacing;
	private int cachedInterval;

	public LeavesTrackTrails(LeavesTrackTrailsInfo info, Actor self)
		: base(info)
	{
		this.cachedInterval = this.Info.StartDelay;
		this.body = self.Trait<BodyOrientation>();
	}

	private WPos cachedPosition;
	protected override void Created(Actor self)
	{
		this.facing = self.TraitOrDefault<IFacing>();
		this.cachedFacing = this.facing?.Facing ?? WAngle.Zero;
		this.cachedPosition = self.CenterPosition;

		base.Created(self);
	}

	private int ticks;
	private int offset;
	private bool wasStationary;
	private bool isMoving;
	private bool previouslySpawned;
	private CPos previousSpawnCell;
	private WAngle previousSpawnFacing;

	void ITick.Tick(Actor self)
	{
		if (this.IsTraitDisabled)
			return;

		this.wasStationary = !this.isMoving;
		this.isMoving = self.CenterPosition != this.cachedPosition;
		if (this.isMoving && !this.Info.TrailWhileMoving || !this.isMoving && !this.Info.TrailWhileStationary)
			return;

		if (this.isMoving == this.wasStationary && this.Info.StartDelay > -1)
		{
			this.cachedInterval = this.Info.StartDelay;
			this.ticks = 0;
		}

		if (++this.ticks >= this.cachedInterval)
		{
			var spawnCell = this.Info.SpawnAtLastPosition ? self.World.Map.CellContaining(this.cachedPosition) : self.World.Map.CellContaining(self.CenterPosition);
			if (!self.World.Map.Contains(spawnCell))
				return;

			var type = self.World.Map.GetTerrainInfo(spawnCell).Type;

			if (++this.offset >= this.Info.Offsets.Length)
				this.offset = 0;

			if (this.Info.TerrainTypes.Count == 0 || this.Info.TerrainTypes.Contains(type))
			{
				var spawnFacing = this.Info.SpawnAtLastPosition ? this.cachedFacing : this.facing?.Facing ?? WAngle.Zero;

				if (!this.Info.ChangeFacingInstantly && this.previouslySpawned && this.previousSpawnCell == spawnCell)
					spawnFacing = this.previousSpawnFacing;

				var offsetRotation = this.Info.Offsets[this.offset].Rotate(this.body.QuantizeOrientation(self.Orientation));
				var spawnPosition = this.Info.SpawnAtLastPosition ? this.cachedPosition : self.CenterPosition;
				var pos = this.Info.Type == TrailType.CenterPosition ? spawnPosition + this.body.LocalToWorld(offsetRotation) :
					self.World.Map.CenterOfCell(spawnCell);

				self.World.AddFrameEndTask(w => w.Add(new SpriteEffect(pos, spawnFacing, self.World, this.Info.Image,
					this.Info.Sequences.Random(Game.CosmeticRandom), this.Info.Palette, this.Info.VisibleThroughFog)));

				this.previouslySpawned = true;
				this.previousSpawnCell = spawnCell;
				this.previousSpawnFacing = spawnFacing;
			}

			this.cachedPosition = self.CenterPosition;
			this.cachedFacing = this.facing?.Facing ?? WAngle.Zero;
			this.ticks = 0;

			this.cachedInterval = this.isMoving ? this.Info.MovingInterval : this.Info.StationaryInterval;
		}
	}

	protected override void TraitEnabled(Actor self)
	{
		this.cachedPosition = self.CenterPosition;
	}
}
