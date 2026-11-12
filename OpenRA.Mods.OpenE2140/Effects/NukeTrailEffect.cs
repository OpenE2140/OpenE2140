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

using OpenRA.Effects;
using OpenRA.Graphics;

namespace OpenRA.Mods.OpenE2140.Effects;

public class NukeTrailEffect : IEffect, ISpatiallyPartitionable
{
	private readonly World world;
	private readonly string palette;
	private readonly Animation anim;
	private readonly int zOffset;
	private readonly bool visibleThroughFog;
	private readonly WPos pos;

	private int delay;
	private bool initialized;

	public NukeTrailEffect(WPos pos, World world, string image, string sequence, string palette, int zOffset,
		bool visibleThroughFog = false, int delay = 0)
	{
		this.world = world;
		this.palette = palette;
		this.zOffset = zOffset;
		this.visibleThroughFog = visibleThroughFog;
		this.pos = pos;
		this.delay = delay;

		this.anim = new Animation(world, image);
		this.anim.PlayThen(sequence, () => world.AddFrameEndTask(w => { w.Remove(this); w.ScreenMap.Remove(this); }));
	}

	public void Tick(World world)
	{
		if (this.delay-- > 0)
			return;

		if (!this.initialized)
		{
			world.ScreenMap.Add(this, this.pos, this.anim.Image);
			this.initialized = true;
		}
		else
		{
			this.anim.Tick();
			world.ScreenMap.Update(this, this.pos, this.anim.Image);
		}
	}

	public IEnumerable<IRenderable> Render(WorldRenderer wr)
	{
		if (!this.initialized || (!this.visibleThroughFog && this.world.FogObscures(this.pos)))
			return SpriteRenderable.None;

		return this.anim.Render(this.pos, WVec.Zero, this.zOffset, wr.Palette(this.palette));
	}
}
