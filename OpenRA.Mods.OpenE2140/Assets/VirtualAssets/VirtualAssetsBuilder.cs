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

using System.Text.RegularExpressions;
using OpenRA.Primitives;

namespace OpenRA.Mods.OpenE2140.Assets.VirtualAssets;

public static class VirtualAssetsBuilder
{
	private record FrameWithPalette(Rectangle Bounds, byte[] Pixels, Color[] Palette);

	private record FrameInfo(int Frame, int2 Offset, bool FlipX);

	public static IEnumerable<(Rectangle Bounds, byte[] Pixels)> BuildSpriteSheet(VirtualAssetsStream stream)
	{
		var inputFrames = stream.Source.Frames.Select(
				frame => new FrameWithPalette(
					new Rectangle(frame.Width / -2, frame.Height / -2, frame.Width, frame.Height),
					frame.Pixels,
					stream.Source.Palettes[frame.Palette].Colors
				)
			)
			.ToArray();

		var globalEffects = stream.Node.Value.Value == null ? Array.Empty<string>() : stream.Node.Value.Value.Split(" ", StringSplitOptions.RemoveEmptyEntries);
		var outputFrames = new List<(Rectangle Bounds, byte[] Pixels)>();

		foreach (var animationNode in stream.Node.Value.Nodes)
		{
			var chunks = animationNode.Value.Value.Split(" ", StringSplitOptions.RemoveEmptyEntries);

			if (chunks.Length == 0)
				throw new Exception("Broken format!");

			var frameSelector = string.Empty;
			var facings = 1;
			var cycles = 1;
			var frameEffects = new List<string>();

			for (var i = 0; i < chunks.Length; i++)
			{
				switch (i)
				{
					case 0:
						frameSelector = chunks[i];

						break;

					case 1 when int.TryParse(chunks[i], out var newFacings):
						facings = newFacings;

						break;

					case 2 when int.TryParse(chunks[i], out var newCycles):
						cycles = newCycles;

						break;

					default:
						frameEffects.Add(chunks[i]);

						break;
				}
			}

			var offsets = animationNode.Value.Nodes.FirstOrDefault(e => e.Key == "Offsets")?.Value.Value;

			var frameInfos = VirtualAssetsBuilder.BuildFrameInfos(frameSelector, facings, offsets);

			foreach (var frameInfo in frameInfos)
			{
				for (var cycle = 0; cycle < cycles; cycle++)
				{
					var frame = inputFrames[frameInfo.Frame];
					var palette = frame.Palette;

					try
					{
						palette = globalEffects.Concat(frameEffects)
							.Aggregate(palette, (current, effect) => VirtualAssetsBuilder.ApplyPalette(stream.PaletteEffects[effect], current, cycle));

						var pixels = frame.Pixels;

						if (frameInfo.FlipX)
						{
							pixels = new byte[pixels.Length];

							for (var y = 0; y < frame.Bounds.Height; y++)
							{
								Array.Copy(
									frame.Pixels.Skip(y * frame.Bounds.Width).Take(frame.Bounds.Width).Reverse().ToArray(),
									0,
									pixels,
									y * frame.Bounds.Width,
									frame.Bounds.Width
								);
							}
						}

						outputFrames.Add(
							new ValueTuple<Rectangle, byte[]>(
								new Rectangle(
									(frameInfo.FlipX ? -frameInfo.Offset.X - frame.Bounds.Width % 2 : frameInfo.Offset.X) + frame.Bounds.X,
									frameInfo.Offset.Y + frame.Bounds.Y,
									frame.Bounds.Width,
									frame.Bounds.Height
								),
								pixels.Select(e => palette[e]).SelectMany(e => new[] { e.R, e.G, e.B, e.A }).ToArray()
							)
						);
					}
					catch (Exception e)
					{
						Console.WriteLine(e);
					}
				}
			}
		}

		return outputFrames;
	}

	private static Color[] ApplyPalette(VirtualPalette paletteEffect, IReadOnlyCollection<Color> basePalette, int cycle)
	{
		var palette = paletteEffect.Application switch
		{
			VirtualPalette.Mode.Replace => new Color[basePalette.Count],
			VirtualPalette.Mode.Merge => basePalette.ToArray(),
			_ => throw new Exception("Unsupported PaletteApplication!")
		};

		var colors = paletteEffect.Colors[cycle % paletteEffect.Colors.Length];

		for (var i = 0; i < colors.Length; i++)
		{
			var oldColor = palette[colors[i].Index];
			var newColor = colors[i].Color;

			palette[colors[i].Index] = paletteEffect.ColorEffect switch
			{
				VirtualPalette.Effect.Normal => newColor,
				VirtualPalette.Effect.Multiply => Color.FromArgb(
					oldColor.A * newColor.A / byte.MaxValue,
					oldColor.R * newColor.R / byte.MaxValue,
					oldColor.G * newColor.G / byte.MaxValue,
					oldColor.B * newColor.B / byte.MaxValue
				),
				_ => throw new Exception("Unsupported ColorEffect!")
			};
		}

		return palette;
	}

	private static List<FrameInfo> BuildFrameInfos(string frames, int facings, string? offsets)
	{
		var frameOffsets = new Dictionary<int, int2>();

		if (offsets != null)
		{
			foreach (var segment in offsets.Split(' ', StringSplitOptions.RemoveEmptyEntries))
			{
				var match = Regex.Match(segment, "^(\\d+(?:,\\d+|-\\d+)*):(-?\\d+),(-?\\d+)$");

				if (!match.Success)
					throw new Exception("Broken format!");

				var offset = new int2(int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value));

				foreach (var rangeSegment in match.Groups[1].Value.Split(','))
				{
					var rangeParts = rangeSegment.Split('-');
					var first = int.Parse(rangeParts[0]);
					var last = rangeParts.Length < 2 ? first : int.Parse(rangeParts[1]);

					for (var i = first; i <= last; i++)
						frameOffsets.Add(i, offset);
				}
			}
		}

		var frameInfos = SequenceParser.Parse(frames)
			.Select(frame => new FrameInfo(frame, frameOffsets.TryGetValue(frame, out var offset) ? offset : new int2(), false))
			.ToList();

		if (facings <= 1)
			return frameInfos;

		var framesPerFacing = frameInfos.Count / (facings / 2 + 1);

		for (var facing = facings / 2 - 1; facing > 0; facing--)
		for (var j = 0; j < framesPerFacing; j++)
			frameInfos.Add(frameInfos[facing * framesPerFacing + j] with { FlipX = true });

		return frameInfos;
	}
}
