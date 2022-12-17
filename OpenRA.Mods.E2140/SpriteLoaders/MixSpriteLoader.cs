using System.IO;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.E2140.FileFormats;
using OpenRA.Primitives;

namespace OpenRA.Mods.E2140.SpriteLoaders
{
	public class MixSpriteFrame : ISpriteFrame
	{
		public SpriteFrameType Type { get; }
		public Size Size { get; }
		public Size FrameSize { get; }
		public float2 Offset { get; }
		public byte[] Data { get; }
		public bool DisableExportPadding => true;

		public MixSpriteFrame(MixFrame frame)
		{
			Type = frame.Is32bpp ? SpriteFrameType.Rgba32 : SpriteFrameType.Indexed8;
			Size = new Size(frame.Width, frame.Height);
			FrameSize = new Size(frame.Width, frame.Height);
			Offset = new int2(0, 0);
			Data = frame.Pixels;
		}
	}

	public class MixSpriteLoader : ISpriteLoader
	{
		public bool TryParseSprite(Stream s, string filename, out ISpriteFrame[] frames, out TypeDictionary metadata)
		{
			var start = s.Position;
			var identifier = s.ReadASCII(10);
			s.Position = start;

			if (identifier != "MIX FILE  ")
			{
				metadata = null;
				frames = null;
				return false;
			}

			var mix = new Mix(s);

			if (mix.Frames.Length == 0)
			{
				metadata = null;
				frames = null;
				return false;
			}

			metadata = null;
			frames = mix.Frames.Select(frame => new MixSpriteFrame(frame)).ToArray();

			return true;
		}
	}
}
