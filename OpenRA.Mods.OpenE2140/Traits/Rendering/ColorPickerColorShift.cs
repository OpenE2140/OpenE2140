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

using JetBrains.Annotations;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.E2140.Traits.Rendering;

[TraitLocation(SystemActors.World | SystemActors.EditorWorld)]
[Desc("Create a color picker palette from another palette.")]
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class ColorPickerColorShiftInfo : TraitInfo
{
	[PaletteReference]
	[FieldLoader.RequireAttribute]
	[Desc("The name of the palette to base off.")]
	public readonly string BasePalette = "";

	[Desc("Hues between this and MaxHue will be shifted.")]
	public readonly float MinHue = 0.83f;

	[Desc("Hues between MinHue and this will be shifted.")]
	public readonly float MaxHue = 0.84f;

	[Desc("Hue reference for the color shift.")]
	public readonly float ReferenceHue = 0.835f;

	[Desc("Saturation reference for the color shift.")]
	public readonly float ReferenceSaturation = 1;

	public override object Create(ActorInitializer init) { return new ColorPickerColorShift(this); }
}

public class ColorPickerColorShift : ILoadsPalettes, ITickRender
{
	private readonly ColorPickerColorShiftInfo info;
	private readonly ColorPickerManagerInfo colorManager;
	private Color color;

	public ColorPickerColorShift(ColorPickerColorShiftInfo info)
	{
		this.colorManager = Game.ModData.DefaultRules.Actors[SystemActors.World].TraitInfo<ColorPickerManagerInfo>();
		this.info = info;
	}

	void ILoadsPalettes.LoadPalettes(WorldRenderer worldRenderer)
	{
		this.color = this.colorManager.Color;
		var (_, hue, saturation, _) = this.color.ToAhsv();

		worldRenderer.SetPaletteColorShift(
			this.info.BasePalette,
			hue - this.info.ReferenceHue,
			saturation - this.info.ReferenceSaturation,
			this.info.MinHue,
			this.info.MaxHue
		);
	}

	void ITickRender.TickRender(WorldRenderer worldRenderer, Actor self)
	{
		if (this.color == this.colorManager.Color)
			return;

		this.color = this.colorManager.Color;
		var (_, hue, saturation, _) = this.color.ToAhsv();

		worldRenderer.SetPaletteColorShift(
			this.info.BasePalette,
			hue - this.info.ReferenceHue,
			saturation - this.info.ReferenceSaturation,
			this.info.MinHue,
			this.info.MaxHue
		);
	}
}
