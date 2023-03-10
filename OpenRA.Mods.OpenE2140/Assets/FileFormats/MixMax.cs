using OpenRA.Primitives;

namespace OpenRA.Mods.OpenE2140.Assets.FileFormats;

public class MixMax
{
	public record Frame(Size Size, byte[] Pixels);

	public readonly Frame[] Frames;

	public MixMax(Stream stream)
	{
		var frames = new List<Frame>();

		for (var size = 1; size <= 4; size <<= 1)
		for (var i = 0; i < 256; i++)
		{
			var pixels = new byte[size * size * 4];

			for (var j = 0; j < pixels.Length; j += 4)
			{
				var color16 = stream.ReadUInt16();
				pixels[j + 0] = (byte)((color16 & 0xf800) >> 8);
				pixels[j + 1] = (byte)((color16 & 0x07e0) >> 3);
				pixels[j + 2] = (byte)((color16 & 0x001f) << 3);
				pixels[j + 3] = 0xff;
			}

			frames.Add(new Frame(new Size(size, size), pixels));
		}

		this.Frames = frames.ToArray();
	}
}
