using System.Collections.Generic;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.E2140.Traits.World
{
    public class MuzzlePaletteInfo : TraitInfo
    {
        [FieldLoader.Require]
        [PaletteDefinition]
        [Desc("Palette name used internally.")]
        public readonly string Name = null;

        public readonly bool AllowModifiers = true;

        public override object Create(ActorInitializer init) { return new MuzzlePalette(init, this); }
    }

    public class MuzzlePalette : ILoadsPalettes, IProvidesAssetBrowserPalettes
    {
        private MuzzlePaletteInfo info;
        private OpenRA.World world;

        public IEnumerable<string> PaletteNames { get { yield return info.Name; } }

        public MuzzlePalette(ActorInitializer init, MuzzlePaletteInfo info)
        {
            world = init.World;
            this.info = info;
        }

        public void LoadPalettes(WorldRenderer worldRenderer)
        {
            var colors = new uint[Palette.Size];

            colors[244] = 0xffff9e52;
            colors[245] = 0xffefb68c;
            colors[246] = 0xffffebc6;
            colors[247] = 0xffffffff;

            worldRenderer.AddPalette(info.Name, new ImmutablePalette(colors), info.AllowModifiers);
        }
    }
}
