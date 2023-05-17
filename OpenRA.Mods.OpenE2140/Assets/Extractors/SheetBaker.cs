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

using OpenRA.Primitives;

namespace OpenRA.Mods.OpenE2140.Assets.Extractors;

public class SheetBaker
{
	public record Entry(Rectangle Rectangle, byte[] Pixels);

	public readonly List<Entry> Frames = new List<Entry>();
	private readonly int channels;

	public SheetBaker(int channels)
	{
		this.channels = channels;
	}

	public byte[] Bake(out int width, out int height, out int offsetX, out int offsetY)
	{
		if (!this.Frames.Any())
		{
			width = 1;
			height = 1;
			offsetX = 0;
			offsetY = 0;

			return new byte[this.channels];
		}

		var location = new int2(this.Frames.Min(f => f.Rectangle.Left), this.Frames.Min(f => f.Rectangle.Top));

		var size = new Size(this.Frames.Max(f => f.Rectangle.Right - location.X), this.Frames.Max(f => f.Rectangle.Bottom - location.Y));

		var frameRectangle = new Rectangle(location, size);
		var framesX = (int)Math.Ceiling(Math.Sqrt(this.Frames.Count));
		var framesY = (int)Math.Ceiling(this.Frames.Count / (float)framesX);

		width = framesX * frameRectangle.Width;
		height = framesY * frameRectangle.Height;
		offsetX = frameRectangle.X;
		offsetY = frameRectangle.Y;

		var data = new byte[width * height * this.channels];

		for (var i = 0; i < this.Frames.Count; i++)
		{
			var frame = this.Frames[i];

			for (var y = 0; y < frame.Rectangle.Height; y++)
			{
				Array.Copy(
					frame.Pixels,
					y * frame.Rectangle.Width * this.channels,
					data,
					((i / framesX * frameRectangle.Height + y - frameRectangle.Y + frame.Rectangle.Y) * width
						+ i % framesX * frameRectangle.Width
						- frameRectangle.X
						+ frame.Rectangle.X)
					* this.channels,
					frame.Rectangle.Width * this.channels
				);
			}
		}

		return data;
	}
}
