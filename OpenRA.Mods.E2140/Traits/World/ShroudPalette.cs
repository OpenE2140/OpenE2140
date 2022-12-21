using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.E2140.Traits
{
	[TraitLocation(SystemActors.World | SystemActors.EditorWorld)]
	[Desc("Adds the hard-coded shroud palette to the game")]
	class ShroudPaletteInfo : TraitInfo
	{
		[PaletteDefinition]
		[FieldLoader.Require]
		[Desc("Internal palette name")]
		public readonly string Name = "shroud";

		[Desc("Palette type")]
		public readonly bool Fog = false;

		public override object Create(ActorInitializer init) { return new ShroudPalette(this); }
	}

	class ShroudPalette : ILoadsPalettes, IProvidesAssetBrowserPalettes
	{
		readonly ShroudPaletteInfo info;

		public ShroudPalette(ShroudPaletteInfo info) { this.info = info; }

		public void LoadPalettes(WorldRenderer wr)
		{
			var c = info.Fog ? Fog : Shroud;
			wr.AddPalette(info.Name, new ImmutablePalette(Enumerable.Range(0, Palette.Size).Select(i => (uint)c[i % c.Length].ToArgb())));
		}

		static readonly Color[] Fog = new[]
		{
			Color.FromArgb(0, 0, 0, 0),
			Color.FromArgb(60, 0, 0, 0),
			Color.FromArgb(0, 0, 0, 0),
			Color.FromArgb(60, 0, 0, 0),
		};

		static readonly Color[] Shroud = new[]
		{
			Color.FromArgb(255, 0, 0, 0),

			Color.FromArgb(0, 0, 0, 0),
			Color.FromArgb(128, 0, 0, 0),
			Color.FromArgb(32, 0, 0, 0),
			Color.FromArgb(255, 0, 0, 0),
		};

		public IEnumerable<string> PaletteNames { get { yield return info.Name; } }
	}
}
