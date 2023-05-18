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

using JetBrains.Annotations;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.PlayerColor;

[TraitLocation(SystemActors.World | SystemActors.EditorWorld)]
[Desc("Create a color picker palette from another palette.")]
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class ColorPickerColorShiftInfo : TraitInfo
{
	[PaletteReference]
	[FieldLoader.Require]
	[Desc("The name of the palette to base off.")]
	public readonly string BasePalette = string.Empty;

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

public class ColorPickerColorShift : ITickRender
{
	private readonly ColorPickerColorShiftInfo info;
	private readonly ColorPickerManagerInfo colorManager;
	private Color? color;
	private Color? newColor;

	public ColorPickerColorShift(ColorPickerColorShiftInfo info)
	{
		this.colorManager = Game.ModData.DefaultRules.Actors[SystemActors.World].TraitInfo<ColorPickerManagerInfo>();
		this.colorManager.OnColorPickerColorUpdate += color => this.newColor = color;
		this.info = info;
	}

	void ITickRender.TickRender(WorldRenderer worldRenderer, Actor self)
	{
		if (this.newColor == null || this.newColor == this.color)
			return;

		this.color = this.newColor.Value;
		this.newColor = null;
		var (_, hue, saturation, value) = this.color.Value.ToAhsv();

		worldRenderer.SetPaletteColorShift(
			this.info.BasePalette,
			hue - this.info.ReferenceHue,
			saturation - this.info.ReferenceSaturation,
			value,
			this.info.MinHue,
			this.info.MaxHue
		);
	}
}
