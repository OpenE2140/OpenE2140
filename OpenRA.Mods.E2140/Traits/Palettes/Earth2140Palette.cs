#region Copyright & License Information

/*
 * Copyright 2007-2022 The Earth 2140 Developers (see AUTHORS)
 * This file is part of OpenKrush, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */

#endregion

using JetBrains.Annotations;
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.E2140.Traits.Palettes;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[TraitLocation(SystemActors.World | SystemActors.EditorWorld)]
[Desc("Creates the Earth 2140 special effects palette.")]
public class Earth2140PaletteInfo : TraitInfo
{
	[PaletteDefinition]
	[FieldLoader.RequireAttribute]
	[Desc("Internal palette name")]
	public readonly string? Name;

	public readonly bool AllowModifiers = true;

	public override object Create(ActorInitializer init) { return new Earth2140Palette(this); }
}

public class Earth2140Palette : ILoadsPalettes
{
	private readonly Earth2140PaletteInfo info;

	public Earth2140Palette(Earth2140PaletteInfo info)
	{
		this.info = info;
	}

	public void LoadPalettes(WorldRenderer worldRenderer)
	{
		var colors = new uint[Palette.Size];

		// Tracks
		colors[240] = (uint)Color.FromArgb(0xff181c18).ToArgb();
		colors[241] = (uint)Color.FromArgb(0xff212421).ToArgb();
		colors[242] = (uint)Color.FromArgb(0xff181c18).ToArgb();
		colors[243] = (uint)Color.FromArgb(0xff292c29).ToArgb();

		// Muzzle flash.
		colors[244] = (uint)Color.FromArgb(0xffff9e52).ToArgb();
		colors[245] = (uint)Color.FromArgb(0xffefb68c).ToArgb();
		colors[246] = (uint)Color.FromArgb(0xffffebc6).ToArgb();
		colors[247] = (uint)Color.FromArgb(0xffffffff).ToArgb();

		// Player colors
		for (var i = 248; i <= 252; i++)
		{
			var gray = 0x80 + (i - 250) * 0x18;

			colors[i] = (uint)Color.FromArgb(0xff, gray, gray, gray).ToArgb();
		}

		worldRenderer.AddPalette(this.info.Name, new(colors), this.info.AllowModifiers);
	}
}
