﻿#region Copyright & License Information

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
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.OpenE2140.Graphics;
using OpenRA.Mods.OpenE2140.Helpers.Reflection;

namespace OpenRA.Mods.OpenE2140.Traits.Rendering.SpriteCutOff;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("Cutoff sprites before rendering, used for the elevator.")]
public class RenderElevatorSpritesInfo : RenderSpritesInfo
{
	public override object Create(ActorInitializer init)
	{
		return new RenderElevatorSprites(init, this);
	}
}

public class RenderElevatorSprites : RenderSprites
{
	private readonly RenderSpritesReflectionHelper reflectionHelper;

	public RenderElevatorSprites(ActorInitializer init, RenderSpritesInfo info)
		: base(init, info)
	{
		this.reflectionHelper = new RenderSpritesReflectionHelper(this);
	}

	public override IEnumerable<IRenderable> Render(Actor self, WorldRenderer worldRenderer)
	{
		var renderables = this.reflectionHelper.RenderAnimations(
			self,
			worldRenderer,
			this.reflectionHelper.GetVisibleAnimations(),
			(anim, renderables) =>
			{
				if (anim is CutOffAnimationWithOffset elevatorAnimation)
					SpriteCutOffHelper.ApplyCutOff(renderables, _ => elevatorAnimation.CutOff(), elevatorAnimation.Direction);
			}
		);

		return renderables;
	}
}
