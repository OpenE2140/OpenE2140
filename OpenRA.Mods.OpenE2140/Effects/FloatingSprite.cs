#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

namespace OpenRA.Mods.OpenE2140.Effects;

using OpenRA.Effects;
using OpenRA.Graphics;
using System.Collections.Generic;
using Util = Common.Util;

public sealed class FloatingSprite : IEffect, ISpatiallyPartitionable
{
	private readonly WDist[] speed;
	private readonly WDist[] gravity;
	private readonly Animation anim;

	private readonly bool visibleThroughFog;
	private readonly int turnRate;
	private readonly int randomRate;
	private readonly string palette;

	private WPos pos;
	private WVec offset;
	private int lifetime;
	private int ticks;
	private WAngle facing;

	public FloatingSprite(Actor emitter, string image, string[] sequences, string palette, bool isPlayerPalette,
		int[] lifetime, WDist[] speed, WDist[] gravity, int turnRate, int randomRate, WPos pos, WAngle facing,
		bool visibleThroughFog = false)
	{
		var world = emitter.World;
		this.pos = pos;
		this.turnRate = turnRate;
		this.randomRate = randomRate;
		this.speed = speed;
		this.gravity = gravity;
		this.visibleThroughFog = visibleThroughFog;
		this.facing = facing;

		this.anim = new Animation(world, image, () => facing);
		this.anim.PlayRepeating(sequences.Random(world.LocalRandom));
		world.ScreenMap.Add(this, pos, this.anim.Image);
		this.lifetime = Util.RandomInRange(world.LocalRandom, lifetime);

		this.palette = isPlayerPalette ? palette + emitter.Owner.InternalName : palette;
	}

	public void Tick(World world)
	{
		if (--this.lifetime < 0)
		{
			world.AddFrameEndTask(w => { w.Remove(this); w.ScreenMap.Remove(this); });
			return;
		}

		if (--this.ticks < 0)
		{
			var forward = Util.RandomDistance(world.LocalRandom, this.speed).Length;
			var height = Util.RandomDistance(world.LocalRandom, this.gravity).Length;

			this.offset = new WVec(forward, 0, height);

			if (this.turnRate > 0)
				this.facing = WAngle.FromFacing(Util.NormalizeFacing(this.facing.Facing + world.LocalRandom.Next(-this.turnRate, this.turnRate)));

			this.offset = this.offset.Rotate(WRot.FromYaw(this.facing));

			this.ticks = this.randomRate;
		}

		this.anim.Tick();

		this.pos += this.offset;

		world.ScreenMap.Update(this, this.pos, this.anim.Image);
	}

	public IEnumerable<IRenderable> Render(WorldRenderer wr)
	{
		if (!this.visibleThroughFog && wr.World.FogObscures(this.pos))
			return SpriteRenderable.None;

		return this.anim.Render(this.pos, wr.Palette(this.palette));
	}
}