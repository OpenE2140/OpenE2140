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
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Rendering;

[Desc("Debug trait for rendering actor's occupied cells. Attach to world.")]
[TraitLocation(SystemActors.World)]
public class ActorDebugInfo : TraitInfo
{
	public override object Create(ActorInitializer init)
	{
		return new ActorDebug(init.Self.World);
	}
}

public class ActorDebug : IRender
{
	private readonly OpenRA.World world;
	private readonly Sprite sprite;

	public ActorDebug(OpenRA.World world)
	{
		this.world = world;
		this.sprite = this.world.Map.Sequences.GetSequence("overlay", "build-invalid").GetSprite(0);
	}

	IEnumerable<IRenderable> IRender.Render(Actor self, WorldRenderer wr)
	{
		foreach (var actor in self.World.ActorMap.AllActors())
		{
			if (actor.TryGetTrait<IOccupySpace>(out var ios))
			{
				foreach (var p in ios.OccupiedCells())
				{
					yield return new SpriteRenderable(this.sprite, self.World.Map.CenterOfCell(p.Cell), WVec.Zero, -511, null, 1f, 0.5f, float3.Ones, TintModifiers.IgnoreWorldTint, true);
				}
			}
			else
			{
				yield return new SpriteRenderable(this.sprite, self.World.Map.CenterOfCell(actor.Location), WVec.Zero, -511, null, 1f, 0.5f, float3.Ones, TintModifiers.IgnoreWorldTint, true);
			}
		}
	}

	IEnumerable<Rectangle> IRender.ScreenBounds(Actor self, WorldRenderer wr)
	{
		yield break;
	}
}
