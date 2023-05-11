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

using System.Text;
using System.Text.RegularExpressions;
using OpenRA.FileSystem;
using OpenRA.Mods.OpenE2140.Assets.FileFormats;
using OpenRA.Primitives;

namespace OpenRA.Mods.OpenE2140.Assets.VirtualAssets;

public static class VirtualAssetsBuilder
{
	private enum PalleteApplication
	{
		Merge,
		Replace
	}

	public record Frame(Rectangle Bounds, byte[] Pixels);

	private record FrameInfo(int Frame, int2 Offset, bool FlipX);

	private record PaletteEffect((int Index, Color Color)[][] Colors, PalleteApplication Application);

	public const string Identifier = "VirtualSpriteSheet";
	private const string Extension = ".vspr";

	public static readonly Dictionary<string, Frame[]> Cache = new Dictionary<string, Frame[]>();

	public static Dictionary<string, Stream> BuildAssets(IReadOnlyFileSystem? fileSystem, string name)
	{
		var virtualAssets = new Dictionary<string, Stream>();

		if (fileSystem == null || !fileSystem.TryOpen(name, out var yamlStream))
			return virtualAssets;

		var yaml = MiniYaml.FromStream(yamlStream);

		var source = yaml.FirstOrDefault(e => e.Key == "Source")?.Value.Value;
		var palettes = yaml.FirstOrDefault(e => e.Key == "Palettes")?.Value;
		var generate = yaml.FirstOrDefault(e => e.Key == "Generate")?.Value;

		if (source == null || generate == null)
			return virtualAssets;

		var paletteEffects = palettes == null ? new Dictionary<string, PaletteEffect>() : VirtualAssetsBuilder.BuildPaletteEffects(palettes);

		// TODO instead of opening the mix, get the image data along the palette from the engine, allowing to use any sprite source.
		if (!fileSystem.TryOpen(source, out var stream))
			return virtualAssets;

		var mix = new Mix(stream);

		foreach (var node in generate.Nodes)
			virtualAssets.Add(node.Key + VirtualAssetsBuilder.Extension, new MemoryStream(VirtualAssetsBuilder.BuildSpriteSheet(mix, paletteEffects, node)));

		return virtualAssets;
	}

	private static Dictionary<string, PaletteEffect> BuildPaletteEffects(MiniYaml palettes)
	{
		var effects = new Dictionary<string, PaletteEffect>();

		foreach (var paletteNode in palettes.Nodes)
		{
			var name = paletteNode.Key;

			var settings = paletteNode.Value.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			var application = Enum.Parse<PalleteApplication>(settings[0]);

			var frameNodes = paletteNode.Value.Nodes;

			if (frameNodes[0].Value.Nodes.Count == 0)
				frameNodes = new List<MiniYamlNode> { paletteNode };

			var colors = frameNodes.Select(
					frame => frame.Value.Nodes.Select(
							e =>
							{
								var value = e.Value.Value.Split(" ", StringSplitOptions.RemoveEmptyEntries);

								if (value.Length is not 3 and not 4)
									throw new Exception("Broken format!");

								var r = int.Parse(value[0]);
								var g = int.Parse(value[1]);
								var b = int.Parse(value[2]);
								var a = value.Length < 4 ? 0xff : int.Parse(value[3]);

								return (int.Parse(e.Key), Color.FromArgb(a, r, g, b));
							}
						)
						.ToArray()
				)
				.ToArray();

			effects.Add(name, new PaletteEffect(colors, application));
		}

		return effects;
	}

	private static byte[] BuildSpriteSheet(Mix mix, Dictionary<string, PaletteEffect> paletteEffects, MiniYamlNode sheetNode)
	{
		if (!VirtualAssetsBuilder.Cache.ContainsKey(sheetNode.Key))
		{
			var globalEffects = sheetNode.Value.Value == null ? Array.Empty<string>() : sheetNode.Value.Value.Split(" ", StringSplitOptions.RemoveEmptyEntries);
			var frames = new List<Frame>();

			foreach (var animationNode in sheetNode.Value.Nodes)
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
						var mixFrame = mix.Frames[frameInfo.Frame];
						var palette = new Color[256];

						Array.Copy(mix.Palettes[mixFrame.Palette].Colors, 0, palette, 0, palette.Length);

						try
						{
							foreach (var effect in globalEffects.Concat(frameEffects))
								VirtualAssetsBuilder.ApplyPalette(paletteEffects[effect], palette, cycle);

							var pixels = mixFrame.Pixels;

							if (frameInfo.FlipX)
							{
								pixels = new byte[pixels.Length];

								for (var y = 0; y < mixFrame.Height; y++)
								{
									Array.Copy(
										mixFrame.Pixels.Skip(y * mixFrame.Width).Take(mixFrame.Width).Reverse().ToArray(),
										0,
										pixels,
										y * mixFrame.Width,
										mixFrame.Width
									);
								}
							}

							frames.Add(
								new Frame(
									new Rectangle(
										(frameInfo.FlipX ? -frameInfo.Offset.X : frameInfo.Offset.X) - mixFrame.Width / 2,
										frameInfo.Offset.Y - mixFrame.Height / 2,
										mixFrame.Width,
										mixFrame.Height
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

			VirtualAssetsBuilder.Cache.Add(sheetNode.Key, frames.ToArray());
		}

		var stream = new MemoryStream();
		var writer = new BinaryWriter(stream);
		writer.Write(Encoding.ASCII.GetBytes(VirtualAssetsBuilder.Identifier));
		writer.Write(sheetNode.Key.Length);
		writer.Write(Encoding.ASCII.GetBytes(sheetNode.Key));

		return stream.ToArray();
	}

	private static void ApplyPalette(PaletteEffect paletteEffect, Color[] palette, int cycle)
	{
		if (paletteEffect.Application == PalleteApplication.Replace)
			Array.Fill(palette, Color.Transparent);

		var colors = paletteEffect.Colors[cycle % paletteEffect.Colors.Length];

		for (var i = 0; i < colors.Length; i++)
			palette[colors[i].Index] = colors[i].Color;
	}

	private static List<FrameInfo> BuildFrameInfos(string frames, int facings, string? offsets)
	{
		var frameInfos = new List<FrameInfo>();

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

		foreach (var segment in frames.Split(',', StringSplitOptions.RemoveEmptyEntries))
		{
			var match = Regex.Match(segment, "^(\\d+)(?:-(\\d+)(?:\\[(\\d+):(\\d+):(\\d+)\\])?)?$");

			if (!match.Success)
				throw new Exception("Broken format!");

			var firstFrame = int.Parse(match.Groups[1].Value);
			var lastFrame = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : firstFrame;
			var skipBefore = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
			var take = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : 1;
			var skipAfter = match.Groups[5].Success ? int.Parse(match.Groups[5].Value) : 0;

			var numFrames = Math.Abs(firstFrame - lastFrame) + 1;
			var direction = firstFrame < lastFrame ? 1 : -1;
			var segmentLength = skipBefore + take + skipAfter;
			var trailing = segmentLength - skipAfter;

			for (var i = 0; i < numFrames; i++)
			{
				var frame = firstFrame + i * direction;
				var segmentIndex = i % segmentLength;

				if (segmentIndex >= skipBefore && segmentIndex < trailing)
					frameInfos.Add(new FrameInfo(frame, frameOffsets.TryGetValue(frame, out var offset) ? offset : new int2(), false));
			}
		}

		if (facings <= 1)
			return frameInfos;

		var framesPerFacing = frameInfos.Count / (facings / 2 + 1);

		for (var facing = facings / 2 - 1; facing > 0; facing--)
		for (var j = 0; j < framesPerFacing; j++)
			frameInfos.Add(frameInfos[facing * framesPerFacing + j] with { FlipX = true });

		return frameInfos;
	}
}
