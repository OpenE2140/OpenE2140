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
using OpenRA.GameRules;
using OpenRA.Graphics;

namespace OpenRA.Mods.OpenE2140.Effects;

public class DelayedProjectile : IEffect
{
	private readonly IProjectile projectile;
	private int delay;

	public DelayedProjectile(IProjectile projectile, int delay)
	{
		this.projectile = projectile;
		this.delay = delay;
	}

	public void Tick(World world)
	{
		if (--this.delay <= 0)
			world.AddFrameEndTask(w => { w.Remove(this); w.Add(this.projectile); });
	}

	public IEnumerable<IRenderable> Render(WorldRenderer wr) { yield break; }
}
