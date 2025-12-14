using OpenRA.FileFormats;
using OpenRA.Graphics;
using OpenRA.Mods.OpenE2140.Assets.Extractors;
using OpenRA.Primitives;
using OpenRA.Video;

namespace OpenRA.Mods.OpenE2140.Widgets.Logic;

public static class VideoExtractor
{
	public static void ExtractVideo(IVideo video, string name)
	{
		var outputFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "OpenE2140Extracted", $"{name}.png");

		Directory.CreateDirectory(Path.GetDirectoryName(outputFile) ?? string.Empty);

		var sheetBaker = new SheetBaker(4);

		var originalFrameIndex = video.CurrentFrameIndex;

		video.Reset();

		var width = video.Width;
		var height = video.Height;
		var sourceWidth = Exts.NextPowerOf2(video.Width);

		for (var i = 0; i < video.FrameCount; i++)
		{
			video.AdvanceFrame();

			var frame = new byte[width * height * 4];
			for (var y = 0; y < height; y++)
			{
				Array.Copy(
					video.CurrentFrameData, (y * sourceWidth) * 4,
					frame, y * width * 4, width * 4);
			}

			sheetBaker.Frames.Add(
				new SheetBaker.Entry(new Rectangle(0, 0, width, height), frame)
			);
		}
		SeekVideo(video, originalFrameIndex);

		var data = sheetBaker.Bake(out var finalWidth, out var finalHeight, out var offsetX, out var offsetY, out var frameSize);

		var embeddedData = new Dictionary<string, string> {
			{ "Offset", $"{offsetX},{offsetY}" },
			{ "FrameSize", $"{frameSize.Width},{frameSize.Height}" }
		};

		new Png(data, SpriteFrameType.Bgra32, finalWidth, finalHeight, null, embeddedData).Save(outputFile);

		void SeekVideo(IVideo video, int frameIndex)
		{
			video.Reset();

			var targetFrameIndex = Math.Min(frameIndex, video.FrameCount);
			for (var i = 0; i < targetFrameIndex; i++)
				video.AdvanceFrame();
		}
	}
}
