using System.IO;

namespace OpenRA.Mods.E2140.FileFormats
{
	public class MixPalette
	{
		public readonly uint[] Colors = new uint[256];

		public MixPalette(Stream stream)
		{
			for (var i = 0; i < Colors.Length; i++)
				Colors[i] = (uint)((0xff << 24) | (stream.ReadUInt8() << 16) | (stream.ReadUInt8() << 8) | (stream.ReadUInt8() << 0));

			// Transparent.
			Colors[0] = 0x00000000;

			// Tracks
			Colors[240] = 0xff181c18;
			Colors[241] = 0xff212421;
			Colors[242] = 0xff181c18;
			Colors[243] = 0xff292c29;

			// Muzzle flash.
			Colors[244] = 0x00000000;
			Colors[245] = 0x00000000;
			Colors[246] = 0x00000000;
			Colors[247] = 0x00000000;

			// Shadow.
			Colors[253] = 0x40000000;
			Colors[254] = 0x80000000;
			Colors[255] = 0xc0000000;
		}
	}
}
