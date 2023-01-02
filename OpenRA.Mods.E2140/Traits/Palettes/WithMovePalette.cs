#region Copyright & License Information

/*
 * Copyright 2007-2023 The Earth 2140 Developers (see AUTHORS)
 * This file is part of Earth 2140, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */

#endregion

using JetBrains.Annotations;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.E2140.Traits.Palettes;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("Cycle the tracks pixels when an actor moves.")]
public class WithMovePaletteInfo : TraitInfo, Requires<IMoveInfo>
{
	[PaletteReference(true)]
	[Desc("Custom PlayerColorPalette: BaseName when moving")]
	public readonly string Palette = "playerMove";

	public override object Create(ActorInitializer init) { return new WithMovePalette(init, this); }
}

public class WithMovePalette : IRenderModifier
{
	private readonly WithMovePaletteInfo info;
	private readonly IMove move;
	private readonly RenderSpritesInfo renderSpritesInfo;

	public WithMovePalette(ActorInitializer init, WithMovePaletteInfo info)
	{
		this.info = info;
		this.move = init.Self.Trait<IMove>();
		this.renderSpritesInfo = init.Self.Info.TraitInfo<RenderSpritesInfo>();
	}

	IEnumerable<IRenderable> IRenderModifier.ModifyRender(Actor self, WorldRenderer worldRenderer, IEnumerable<IRenderable> renderables)
	{
		return renderables.Select(
			renderable =>
			{
				if (renderable is IPalettedRenderable { Palette: { } } palettedRenderable
					&& this.move.CurrentMovementTypes.HasFlag(MovementType.Horizontal)
					&& palettedRenderable.Palette.Name == this.renderSpritesInfo.PlayerPalette + self.Owner.InternalName)
					return palettedRenderable.WithPalette(worldRenderer.Palette(this.info.Palette + self.Owner.InternalName));

				return renderable;
			}
		);
	}

	IEnumerable<Rectangle> IRenderModifier.ModifyScreenBounds(Actor self, WorldRenderer worldRenderer, IEnumerable<Rectangle> bounds)
	{
		return bounds;
	}
}
