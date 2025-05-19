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
using OpenRA.Mods.OpenE2140.Traits.World;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Rendering;

[Desc("Overlay for Rush Protection feature visualization. Attach this to the mpspawn actor.")]
public class RushProtectionOverlayInfo : TraitInfo
{
	[Desc("Color of the circle around the protected area")]
	public readonly Color ProtectedAreaCircleColor = Color.Gray;

	public override object Create(ActorInitializer init)
	{
		return new RushProtectionOverlay(this);
	}
}

public class RushProtectionOverlay : IRenderAnnotations, IRender, IWorldLoaded
{
	private readonly RushProtectionOverlayInfo info;
	private RushProtection? rushProtection;

	public RushProtectionOverlay(RushProtectionOverlayInfo rushProtectionOverlayInfo)
	{
		this.info = rushProtectionOverlayInfo;
	}

	void IWorldLoaded.WorldLoaded(OpenRA.World w, WorldRenderer wr)
	{
		this.rushProtection = w.WorldActor.TraitOrDefault<RushProtection>();
	}

	bool IRenderAnnotations.SpatiallyPartitionable => false;

	IEnumerable<IRenderable> IRender.Render(Actor self, WorldRenderer wr)
	{
		if (this.rushProtection?.IsEnabled != true)
			yield break;

		foreach (var protectedPlayer in this.rushProtection.ProtectedPlayers)
		{
			// For all players except the render player, we want to render circle below the fog/shroud, so the circle is only visible,
			// when the area is revealed.
			if (protectedPlayer.Player != self.World.RenderPlayer)
			{
				yield return new CircleRenderable(
					self.World.Map.CenterOfCell(protectedPlayer.SpawnLocation), this.rushProtection.Info.RushProtectionRange, 1, this.info.ProtectedAreaCircleColor);
			}
		}
	}

	IEnumerable<IRenderable> IRenderAnnotations.RenderAnnotations(Actor self, WorldRenderer wr)
	{
		if (this.rushProtection?.IsEnabled != true)
			yield break;

		foreach (var protectedPlayer in this.rushProtection.ProtectedPlayers)
		{
			// We want to render the full circle only for the render player and since annotations are rendered above the shroud, we render the circle here.
			if (protectedPlayer.Player == self.World.RenderPlayer)
			{
				yield return new CircleAnnotationRenderable(
					self.World.Map.CenterOfCell(protectedPlayer.SpawnLocation), this.rushProtection.Info.RushProtectionRange, 1, this.info.ProtectedAreaCircleColor);
			}
		}
	}

	IEnumerable<Rectangle> IRender.ScreenBounds(Actor self, WorldRenderer wr)
	{
		yield break;
	}
}
