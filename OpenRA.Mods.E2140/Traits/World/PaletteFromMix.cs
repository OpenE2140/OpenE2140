using System.Collections.Generic;
using OpenRA.FileSystem;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.E2140.FileFormats;
using OpenRA.Traits;

namespace OpenRA.Mods.E2140.Traits.World
{
	public class PaletteFromMixInfo : TraitInfo, IProvidesCursorPaletteInfo
	{
		[PaletteDefinition]
		[FieldLoader.Require]
		[Desc("Internal palette name")]
		public readonly string Name = null;

		[FieldLoader.Require]
		[Desc("Filename to load")]
		public readonly string Filename = null;

		public readonly int Index = 0;

		public readonly bool AllowModifiers = true;

		[Desc("Whether this palette is available for cursors.")]
		public readonly bool CursorPalette = false;

		public override object Create(ActorInitializer init) { return new PaletteFromMix(init.World, this); }

		string IProvidesCursorPaletteInfo.Palette => CursorPalette ? Name : null;

		ImmutablePalette IProvidesCursorPaletteInfo.ReadPalette(IReadOnlyFileSystem fileSystem)
		{
			return new ImmutablePalette(new Mix(fileSystem.Open(Filename)).Palettes[Index].Colors);
		}
	}

	public class PaletteFromMix : ILoadsPalettes, IProvidesAssetBrowserPalettes
	{
		readonly OpenRA.World world;
		readonly PaletteFromMixInfo info;

		public IEnumerable<string> PaletteNames { get { yield return info.Name; } }

		public PaletteFromMix(OpenRA.World world, PaletteFromMixInfo info)
		{
			this.world = world;
			this.info = info;
		}

		public void LoadPalettes(WorldRenderer wr)
		{
			wr.AddPalette(info.Name, ((IProvidesCursorPaletteInfo)info).ReadPalette(world.Map), info.AllowModifiers);
		}
	}
}
