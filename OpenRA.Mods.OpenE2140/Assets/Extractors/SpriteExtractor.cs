#region Copyright & License Information

/*
 * Copyright (c) The OpenE2140 Developers and Contributors
 * This file is part of OpenE2140, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */

#endregion

using OpenRA.FileFormats;
using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Mods.OpenE2140.Assets.Extractors
{
	public static class SpriteExtractor
	{
		public static void Extract(IEnumerable<Sprite> sprites, string name)
		{
			var outputFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "OpenE2140Extracted", $"{name}.png");

			Directory.CreateDirectory(Path.GetDirectoryName(outputFile) ?? string.Empty);

			var sheetBaker = new SheetBaker(4);

			foreach (var sprite in sprites)
			{
				var sheetData = sprite.Sheet.GetData();

				var frame = new byte[sprite.Bounds.Width * sprite.Bounds.Height * 4];

				for (var y = 0; y < sprite.Bounds.Height; y++)
				{
					Array.Copy(
						sheetData,
						((sprite.Bounds.Y + y) * sprite.Sheet.Size.Width + sprite.Bounds.X) * 4,
						frame,
						y * sprite.Bounds.Width * 4,
						sprite.Bounds.Width * 4
					);
				}

				sheetBaker.Frames.Add(
					new SheetBaker.Entry(new Rectangle((int)sprite.Offset.X, (int)sprite.Offset.Y, sprite.Bounds.Width, sprite.Bounds.Height), frame)
				);
			}

			var data = sheetBaker.Bake(out var width, out var height, out var offsetX, out var offsetY, out var frameSize);

			var embeddedData = new Dictionary<string, string> {
			{ "Offset", $"{offsetX},{offsetY}" },
			{ "FrameSize", $"{frameSize.Width},{frameSize.Height}" }
		};

			new Png(data, SpriteFrameType.Bgra32, width, height, null, embeddedData).Save(outputFile);
		}
	}
}

