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
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Miner;

[RequireExplicitImplementation]
internal interface IWallConnectorInfo : ITraitInfoInterface
{
	string GetWallConnectionType();
}

[Desc("Render trait for actors that change sprites if neighbors with the same trait are present.")]
internal sealed class WithAnimatedWallSpriteBodyInfo : WithSpriteBodyInfo, IWallConnectorInfo, Requires<BuildingInfo>
{
	public readonly string Type = "wall";

	public override object Create(ActorInitializer init) { return new WithAnimatedWallSpriteBody(init, this); }

	public override IEnumerable<IActorPreview> RenderPreviewSprites(ActorPreviewInitializer init, string image, int facings, PaletteReference p)
	{
		var adjacent = 0;
		var locationInit = init.GetOrDefault<LocationInit>();
		var neighbourInit = init.GetOrDefault<RuntimeNeighbourInit>();

		if (locationInit != null && neighbourInit != null)
		{
			var location = locationInit.Value;
			foreach (var kv in neighbourInit.Value)
			{
				var haveNeighbour = false;
				foreach (var n in kv.Value)
				{
					var rb = init.World.Map.Rules.Actors[n].TraitInfos<IWallConnectorInfo>().FirstEnabledTraitOrDefault();
					if (rb != null && rb.GetWallConnectionType() == this.Type)
					{
						haveNeighbour = true;
						break;
					}
				}

				if (!haveNeighbour)
					continue;

				if (kv.Key == location + new CVec(0, -1))
					adjacent |= 1;
				else if (kv.Key == location + new CVec(+1, 0))
					adjacent |= 2;
				else if (kv.Key == location + new CVec(0, +1))
					adjacent |= 4;
				else if (kv.Key == location + new CVec(-1, 0))
					adjacent |= 8;
			}
		}

		var anim = new Animation(init.World, image);
		anim.PlayFetchIndex(RenderSprites.NormalizeSequence(anim, init.GetDamageState(), this.Sequence), () => adjacent);

		yield return new SpriteActorPreview(anim, () => WVec.Zero, () => 0, p);
	}

	string IWallConnectorInfo.GetWallConnectionType()
	{
		return this.Type;
	}
}

internal sealed class WithAnimatedWallSpriteBody : WithSpriteBody, INotifyRemovedFromWorld, IWallConnector, ITick
{
	private readonly WithAnimatedWallSpriteBodyInfo wallInfo;
	private readonly bool createdByMap;

	private int adjacent = 0;
	private bool dirty = true;
	private Construction? construction;

	bool IWallConnector.AdjacentWallCanConnect(Actor self, CPos wallLocation, string wallType, out CVec facing)
	{
		facing = wallLocation - self.Location;
		return this.wallInfo.Type == wallType && Math.Abs(facing.X) + Math.Abs(facing.Y) == 1;
	}

	void IWallConnector.SetDirty() { this.dirty = true; }

	public WithAnimatedWallSpriteBody(ActorInitializer init, WithAnimatedWallSpriteBodyInfo info)
		: base(init, info)
	{
		this.wallInfo = info;
		this.createdByMap = init.Contains<SpawnedByMapInit>();
	}

	protected override void DamageStateChanged(Actor self)
	{
		if (this.IsTraitDisabled)
			return;

		this.DefaultAnimation.PlayFetchIndex(this.NormalizeSequence(self, this.Info.Sequence), () => this.adjacent);
	}

	void ITick.Tick(Actor self)
	{
		if (!this.dirty)
			return;

		// Update connection to neighbours
		this.adjacent = this.CalculateAdjacentIndex(self);

		this.dirty = false;
	}

	private int CalculateAdjacentIndex(Actor self)
	{
		var adjacentActors = CVec.Directions.SelectMany(dir =>
			self.World.ActorMap.GetActorsAt(self.Location + dir));

		var adjacent = 0;
		foreach (var a in adjacentActors)
		{
			var wc = a.TraitsImplementing<IWallConnector>().FirstEnabledTraitOrDefault();
			if (wc == null || !wc.AdjacentWallCanConnect(a, self.Location, this.wallInfo.Type, out var facing))
				continue;

			if (facing.Y > 0)
				adjacent |= 1;
			else if (facing.X < 0)
				adjacent |= 2;
			else if (facing.Y < 0)
				adjacent |= 4;
			else if (facing.X > 0)
				adjacent |= 8;
		}

		return adjacent;
	}

	protected override void TraitEnabled(Actor self)
	{
		base.TraitEnabled(self);

		this.dirty = true;

		if (this.Info.StartSequence == null || this.createdByMap)
		{
			this.DefaultAnimation.PlayFetchIndex(this.NormalizeSequence(self, this.Info.Sequence), () => this.adjacent);
		}
		else
		{
			this.construction = new Construction(
				this.DefaultAnimation,
				() => this.NormalizeSequence(self, this.Info.StartSequence),
				after: () => this.DefaultAnimation.PlayFetchIndex(this.NormalizeSequence(self, this.Info.Sequence), () => this.adjacent));
			this.construction.Start(this.CalculateAdjacentIndex(self));
		}

		UpdateNeighbours(self);

		// Set the initial animation frame before the render tick (for frozen actor previews)
		self.World.AddFrameEndTask(_ => this.DefaultAnimation.Tick());
	}

	private static void UpdateNeighbours(Actor self)
	{
		var adjacentActorTraits = CVec.Directions.SelectMany(dir =>
				self.World.ActorMap.GetActorsAt(self.Location + dir))
			.SelectMany(a => a.TraitsImplementing<IWallConnector>());

		foreach (var aat in adjacentActorTraits)
			aat.SetDirty();
	}

	void INotifyRemovedFromWorld.RemovedFromWorld(Actor self)
	{
		UpdateNeighbours(self);
	}

	private class Construction
	{
		private readonly Animation animation;
		private readonly Func<string> sequenceName;
		private readonly Action after;

		private int timeUntilNextFrame;
		private int startFrame;
		private int currentFrame;
		private int lastFrame;

		public Construction(Animation animation, Func<string> sequenceName, Action after)
		{
			this.animation = animation;
			this.sequenceName = sequenceName;
			this.after = after;
		}

		public void Start(int startFrame)
		{
			var sequence = this.animation.GetSequence(this.sequenceName());
			this.startFrame = startFrame;
			this.currentFrame = startFrame * 3;
			this.lastFrame = this.currentFrame + 3;
			this.timeUntilNextFrame = sequence.Tick;
			this.animation.PlayFetchIndex(this.sequenceName(), this.TickIndex);
		}

		public int TickIndex()
		{
			while (this.timeUntilNextFrame <= 0)
			{
				var sequence = this.animation.GetSequence(this.sequenceName());

				if (++this.currentFrame >= this.lastFrame)
				{
					this.currentFrame = this.startFrame;
					this.after();
					break;
				}
				this.timeUntilNextFrame += sequence.Tick;
			}
			const int T = 40;
			this.timeUntilNextFrame -= T;

			return this.currentFrame;
		}
	}
}
