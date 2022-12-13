using System.Collections.Generic;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.E2140.Traits.World
{
    public class PaletteFromMixInfo : TraitInfo
    {
        [FieldLoader.Require]
        [PaletteDefinition]
        [Desc("Palette name used internally.")]
        public readonly string Name = null;

        [FieldLoader.Require]
        [Desc("Name of the file to load.")]
        public readonly string Filename = null;

        [FieldLoader.Require]
        [Desc("Subpalette to load.")]
        public readonly int Subpalette;

        public readonly bool AllowModifiers = true;

        public override object Create(ActorInitializer init) { return new PaletteFromMix(init, this); }
    }

    public class PaletteFromMix : ILoadsPalettes, IProvidesAssetBrowserPalettes
    {
        private PaletteFromMixInfo info;
        private OpenRA.World world;

        public IEnumerable<string> PaletteNames { get { yield return info.Name; } }

        public PaletteFromMix(ActorInitializer init, PaletteFromMixInfo info)
        {
            world = init.World;
            this.info = info;
        }

        public void LoadPalettes(WorldRenderer worldRenderer)
        {
            using (var stream = world.Map.Open(info.Filename))
            {
                /*var identifier = */stream.ReadASCII(10);
                /*var dataSize = */stream.ReadInt32();
                var numFrames = stream.ReadInt32();
                /*var imagesOffset = */stream.ReadInt32();
                /*var numPalettes = */stream.ReadInt32();
                var firstPaletteId = stream.ReadInt32();
                /*var paletteOffset = */stream.ReadInt32();

                /*var entry = */stream.ReadASCII(5); // "ENTRY"
                stream.Position += numFrames * 4;

                /*var pal = */stream.ReadASCII(5); // " PAL "
                stream.Position += (info.Subpalette - firstPaletteId) * Palette.Size * 3;

                var colors = new uint[Palette.Size];

                for (var i = 0; i < Palette.Size; i++)
                    colors[i] = (uint)((0xff << 24) | (stream.ReadUInt8() << 16) | (stream.ReadUInt8() << 8) | (stream.ReadUInt8() << 0));

                // Transparent.
                colors[0] = 0x00000000;

                // Tracks
                colors[240] = 0xff181c18;
                colors[241] = 0xff212421;
                colors[242] = 0xff181c18;
                colors[243] = 0xff292c29;

                // Muzzle flash.
                colors[244] = 0x00000000;
                colors[245] = 0x00000000;
                colors[246] = 0x00000000;
                colors[247] = 0x00000000;

                // Shadow.
                colors[253] = 0x40000000;
                colors[254] = 0x80000000;
                colors[255] = 0xc0000000;

                worldRenderer.AddPalette(info.Name, new ImmutablePalette(colors), info.AllowModifiers);
            }
        }
    }
}
