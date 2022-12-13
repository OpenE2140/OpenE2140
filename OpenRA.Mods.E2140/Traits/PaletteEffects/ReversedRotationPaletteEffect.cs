using System.Collections.Generic;
using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.E2140.Traits.PaletteEffects
{
	[Desc("Palette effect used for sprinkle \"animations\".")]
	class ReversedRotationPaletteEffectInfo : TraitInfo
	{
		[Desc("Defines to which palettes this effect should be applied to.",
			"If none specified, it applies to all palettes not explicitly excluded.")]
		public readonly HashSet<string> Palettes = new HashSet<string>();

		[Desc("Defines for which tileset IDs this effect should be loaded.",
			"If none specified, it applies to all tileset IDs not explicitly excluded.")]
		public readonly HashSet<string> Tilesets = new HashSet<string>();

		[Desc("Palette index of first RotationRange color.")]
		public readonly int RotationBase = 0x60;

		[Desc("Range of colors to rotate.")]
		public readonly int RotationRange = 7;

		[Desc("Step towards next color index per tick.")]
		public readonly int RotationStep = 1;

		public override object Create(ActorInitializer init) { return new ReversedRotationPaletteEffect(this); }
	}

	class ReversedRotationPaletteEffect : ITick, IPaletteModifier
	{
		readonly ReversedRotationPaletteEffectInfo info;
		readonly uint[] rotationBuffer;
		int ticker;

		public ReversedRotationPaletteEffect(ReversedRotationPaletteEffectInfo info)
		{
			this.info = info;
			rotationBuffer = new uint[info.RotationRange];
		}

		void ITick.Tick(Actor self)
		{
			ticker += info.RotationStep;
		}

		public void AdjustPalette(IReadOnlyDictionary<string, MutablePalette> palettes)
		{
			var rotate = (ticker / 4) % info.RotationRange;
			if (rotate == 0)
				return;

			foreach (var kvp in palettes)
			{
				if (info.Palettes.Count > 0 && !StartsWithAny(kvp.Key, info.Palettes))
					continue;

				var palette = kvp.Value;

				for (var i = 0; i < info.RotationRange; i++)
					rotationBuffer[(info.RotationRange + i - rotate) % info.RotationRange] = palette[info.RotationBase + i];

				for (var i = 0; i < info.RotationRange; i++)
					palette[info.RotationBase + i] = rotationBuffer[i];
			}
		}

		static bool StartsWithAny(string name, HashSet<string> prefixes)
		{
			// PERF: Avoid LINQ.
			foreach (var pref in prefixes)
				if (name.StartsWith(pref))
					return true;

			return false;
		}
	}
}
