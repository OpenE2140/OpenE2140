#region Copyright & License Information

/*
 * Copyright 2007-2022 The Earth 2140 Developers (see AUTHORS)
 * This file is part of OpenKrush, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */

#endregion

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using OpenRA.Graphics;
using OpenRA.Mods.E2140.FileFormats;
using OpenRA.Primitives;

namespace OpenRA.Mods.E2140.SpriteLoaders;

public class MixSpriteFrame : ISpriteFrame
{
	public SpriteFrameType Type { get; }
	public Size Size { get; }
	public Size FrameSize { get; }
	public float2 Offset { get; }
	public byte[] Data { get; }
	public bool DisableExportPadding => true;

	public MixSpriteFrame(SpriteFrameType type, Size size, byte[] pixels)
	{
		this.Type = type;
		this.Size = size;
		this.FrameSize = size;
		this.Offset = new int2(0, 0);
		this.Data = pixels;
	}
}

[UsedImplicitly]
public class MixSpriteLoader : ISpriteLoader
{
	public bool TryParseSprite(Stream s, string filename, [NotNullWhen(true)] out ISpriteFrame[]? frames, out TypeDictionary? metadata)
	{
		var start = s.Position;
		var identifier = s.ReadASCII(10);
		s.Position = start;

		frames = null;
		metadata = null;

		if (identifier != "MIX FILE  ")
			return false;

		var mix = new Mix(s);

		if (mix.Frames.Length == 0)
			return false;

		var framesList = new List<ISpriteFrame>();

		var specialLogic = new[] { "sprb0.mix", "spru0.mix" }.Any(f => filename.EndsWith(f, StringComparison.OrdinalIgnoreCase));

		foreach (var frame in mix.Frames)
		{
			var size = new Size(frame.Width, frame.Height);

			if (frame.Is32Bpp)
				framesList.Add(new MixSpriteFrame(SpriteFrameType.Rgba32, size, frame.Pixels));
			else
			{
				var argbImage = new byte[frame.Pixels.Length * 4];
				var indexedImage = new byte[frame.Pixels.Length];
				var palette = mix.Palettes[frame.Palette].Colors;

				if (specialLogic)
					palette = MixSpriteLoader.ApplySpecialColors(palette);

				for (var i = 0; i < frame.Pixels.Length; i++)
				{
					var index = frame.Pixels[i];
					var color = palette[index];

					if (!specialLogic || index is < 244 or > 247)
					{
						argbImage[i * 4 + 0] = color.R;
						argbImage[i * 4 + 1] = color.G;
						argbImage[i * 4 + 2] = color.B;
						argbImage[i * 4 + 3] = color.A;
					}

					if (specialLogic && index is >= 240 and <= 252)
						indexedImage[i] = index;
				}

				framesList.Add(new MixSpriteFrame(SpriteFrameType.Rgba32, size, argbImage));

				if (specialLogic)
					framesList.Add(new MixSpriteFrame(SpriteFrameType.Indexed8, size, indexedImage));
			}
		}

		frames = framesList.ToArray();

		return true;
	}

	private static Color[] ApplySpecialColors(Color[] palette)
	{
		var newPalette = new Color[palette.Length];
		Array.Copy(palette, newPalette, palette.Length);

		// Tracks
		newPalette[240] = Color.FromArgb(0xff181c18);
		newPalette[241] = Color.FromArgb(0xff212421);
		newPalette[242] = Color.FromArgb(0xff181c18);
		newPalette[243] = Color.FromArgb(0xff292c29);

		// Muzzle flash.
		newPalette[244] = Color.FromArgb(0xffff9e52);
		newPalette[245] = Color.FromArgb(0xffefb68c);
		newPalette[246] = Color.FromArgb(0xffffebc6);
		newPalette[247] = Color.FromArgb(0xffffffff);

		// Player colors
		for (var i = 248; i <= 252; i++)
		{
			var gray = (newPalette[i].R + newPalette[i].G + newPalette[i].B) / 3;

			newPalette[i] = Color.FromArgb(newPalette[i].A, gray, gray, gray);
		}

		return newPalette;
	}
}
