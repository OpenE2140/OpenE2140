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
using OpenRA.Traits;

namespace OpenRA.Mods.E2140.Traits.Palettes;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class ReversedRotationPaletteEffectInfo : TraitInfo
{
	[Desc("Defines to which palettes this effect should be applied to.", "If none specified, it applies to all palettes not explicitly excluded.")]
	public readonly HashSet<string> Palettes = new();

	[Desc("Palette index of first RotationRange color.")]
	public readonly int RotationBase;

	[Desc("Range of colors to rotate.")]
	public readonly int RotationRange;

	[Desc("Step towards next color index per tick.")]
	public readonly int RotationStep = 1;

	public override object Create(ActorInitializer init) { return new ReversedRotationPaletteEffect(this); }
}

public class ReversedRotationPaletteEffect : ITick, IPaletteModifier
{
	private readonly ReversedRotationPaletteEffectInfo info;
	private readonly uint[] rotationBuffer;
	private int ticker;

	public ReversedRotationPaletteEffect(ReversedRotationPaletteEffectInfo info)
	{
		this.info = info;
		this.rotationBuffer = new uint[info.RotationRange];
	}

	void ITick.Tick(Actor self)
	{
		this.ticker += this.info.RotationStep;
	}

	public void AdjustPalette(IReadOnlyDictionary<string, MutablePalette> palettes)
	{
		var rotate = this.ticker / 4 % this.info.RotationRange;

		if (rotate == 0)
			return;

		foreach (var (key, palette) in palettes)
		{
			if (this.info.Palettes.Count > 0 && !ReversedRotationPaletteEffect.StartsWithAny(key, this.info.Palettes))
				continue;

			for (var i = 0; i < this.info.RotationRange; i++)
				this.rotationBuffer[(this.info.RotationRange + i - rotate) % this.info.RotationRange] = palette[this.info.RotationBase + i];

			for (var i = 0; i < this.info.RotationRange; i++)
				palette[this.info.RotationBase + i] = this.rotationBuffer[i];
		}
	}

	private static bool StartsWithAny(string name, IEnumerable<string> prefixes)
	{
		return prefixes.Any(name.StartsWith);
	}
}
