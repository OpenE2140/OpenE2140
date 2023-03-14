#region Copyright & License Information

/*
 * Copyright 2007-2023 The OpenE2140 Developers (see AUTHORS)
 * This file is part of OpenE2140, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */

#endregion

using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Mods.OpenE2140.Graphics;

public class ModifyableSpriteActorPreview : IActorPreview
{
	private readonly Animation animation;
	private readonly Func<WVec> offset;
	private readonly Func<int> zOffset;
	private readonly Func<IRenderable, IRenderable> modify;

	public ModifyableSpriteActorPreview(Animation animation, Func<WVec> offset, Func<int> zOffset, Func<IRenderable, IRenderable> modify)
	{
		this.animation = animation;
		this.offset = offset;
		this.zOffset = zOffset;
		this.modify = modify;
	}

	void IActorPreview.Tick()
	{
		this.animation.Tick();
	}

	IEnumerable<IRenderable> IActorPreview.RenderUI(WorldRenderer wr, int2 pos, float scale)
	{
		return this.animation.RenderUI(wr, pos, this.offset(), this.zOffset(), null, scale).Select(e => this.modify(e));
	}

	IEnumerable<IRenderable> IActorPreview.Render(WorldRenderer wr, WPos pos)
	{
		return this.animation.Render(pos, this.offset(), this.zOffset(), null).Select(e => this.modify(e));
	}

	IEnumerable<Rectangle> IActorPreview.ScreenBounds(WorldRenderer wr, WPos pos)
	{
		yield return this.animation.ScreenBounds(wr, pos, this.offset());
	}
}
