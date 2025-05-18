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

using OpenRA.Activities;
using OpenRA.Mods.Common.Traits.Render;

namespace OpenRA.Mods.OpenE2140.Traits.Resources.Activities;

public class DelayCancelAnimation : Activity
{
	private readonly WithSpriteBody wsb;

	private int tickDelay;

	public DelayCancelAnimation(WithSpriteBody wsb, int delay)
	{
		this.wsb = wsb;
		this.tickDelay = delay;
		this.ChildHasPriority = false;
	}

	public override bool Tick(Actor self)
	{
		if (this.IsCanceling || --this.tickDelay == 0)
		{
			this.wsb.CancelCustomAnimation(self);
		}

		return this.TickChild(self);
	}
}
