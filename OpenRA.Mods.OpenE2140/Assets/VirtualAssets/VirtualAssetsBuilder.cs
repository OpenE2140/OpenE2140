#region Copyright & License Information

/*
 * Copyright 2007-2023 The OpenE2140 Developers (see AUTHORS)
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

// TODO there is a LOT of optimization/refactoring potential here!
public static class VirtualAssetsBuilder
{
	public class Frame
	{
		public readonly Rectangle Bounds;
		public readonly byte[] Pixels;

		public Frame(Rectangle bounds, byte[] pixels)
		{
			this.Bounds = bounds;
			this.Pixels = pixels;
		}
	}

	public const string Identifier = "VirtualSpriteSheet";
	private const string Extension = ".vspr";

	public static readonly Dictionary<string, Frame[]> Cache = new Dictionary<string, Frame[]>();

	private record FrameInfo(int Frame, bool FlipX);

	private static readonly Color[] TracksPalette;
	private static readonly Color[] RotorsPalette;
	private static readonly Color[] EnginesOnPalette;
	private static readonly Color[] EnginesOffPalette;
	private static readonly Color[] MuzzlesPalette;
	private static readonly Color[] FlickerOnPalette;
	private static readonly Color[] FlickerOffPalette;
	private static readonly Color[] LightOffPalette;
	private static readonly Color[] PlayerPalette;
	private static readonly Color[] ShadowsPalette;
	private static readonly Color[] SmokePalette;

	static VirtualAssetsBuilder()
	{
		VirtualAssetsBuilder.TracksPalette = new Color[256];
		VirtualAssetsBuilder.TracksPalette[240] = Color.FromArgb(0xff212421);
		VirtualAssetsBuilder.TracksPalette[241] = Color.FromArgb(0xff181c18);
		VirtualAssetsBuilder.TracksPalette[242] = Color.FromArgb(0xff292c29);
		VirtualAssetsBuilder.TracksPalette[243] = Color.FromArgb(0xff181c18);

		VirtualAssetsBuilder.RotorsPalette = new Color[256];
		VirtualAssetsBuilder.RotorsPalette[240] = Color.FromArgb(0xff000000);
		VirtualAssetsBuilder.RotorsPalette[241] = Color.FromArgb(0xff000000);
		VirtualAssetsBuilder.RotorsPalette[242] = Color.FromArgb(0xff393c39);
		VirtualAssetsBuilder.RotorsPalette[243] = Color.FromArgb(0xff000000);

		VirtualAssetsBuilder.EnginesOnPalette = new Color[256];
		VirtualAssetsBuilder.EnginesOnPalette[240] = Color.FromArgb(0xffff0000);
		VirtualAssetsBuilder.EnginesOnPalette[241] = Color.FromArgb(0xff7b9ebd);
		VirtualAssetsBuilder.EnginesOnPalette[242] = Color.FromArgb(0xff7b9eff);
		VirtualAssetsBuilder.EnginesOnPalette[243] = Color.FromArgb(0xffffffbd);

		VirtualAssetsBuilder.EnginesOffPalette = new Color[256];
		VirtualAssetsBuilder.EnginesOffPalette[240] = Color.FromArgb(0xffff0000);
		VirtualAssetsBuilder.EnginesOffPalette[241] = Color.FromArgb(0xff000000);
		VirtualAssetsBuilder.EnginesOffPalette[242] = Color.FromArgb(0xff000000);
		VirtualAssetsBuilder.EnginesOffPalette[243] = Color.FromArgb(0xff00007b);

		VirtualAssetsBuilder.MuzzlesPalette = new Color[256];
		VirtualAssetsBuilder.MuzzlesPalette[244] = Color.FromArgb(0xffc66d18);
		VirtualAssetsBuilder.MuzzlesPalette[245] = Color.FromArgb(0xffff9e52);
		VirtualAssetsBuilder.MuzzlesPalette[246] = Color.FromArgb(0xffefb68c);
		VirtualAssetsBuilder.MuzzlesPalette[247] = Color.FromArgb(0xffffebc6);

		VirtualAssetsBuilder.FlickerOnPalette = new Color[256];
		VirtualAssetsBuilder.FlickerOnPalette[244] = Color.FromArgb(0xff000000);
		VirtualAssetsBuilder.FlickerOnPalette[245] = Color.FromArgb(0xff000000);
		VirtualAssetsBuilder.FlickerOnPalette[246] = Color.FromArgb(0xfff7ffc6);
		VirtualAssetsBuilder.FlickerOnPalette[247] = Color.FromArgb(0xffff0000);

		VirtualAssetsBuilder.FlickerOffPalette = new Color[256];
		VirtualAssetsBuilder.FlickerOffPalette[244] = Color.FromArgb(0xff000000);
		VirtualAssetsBuilder.FlickerOffPalette[245] = Color.FromArgb(0xff000000);
		VirtualAssetsBuilder.FlickerOffPalette[246] = Color.FromArgb(0xffff0000);
		VirtualAssetsBuilder.FlickerOffPalette[247] = Color.FromArgb(0xff444444);

		VirtualAssetsBuilder.LightOffPalette = new Color[256];
		VirtualAssetsBuilder.LightOffPalette[244] = Color.FromArgb(0xff000000);
		VirtualAssetsBuilder.LightOffPalette[245] = Color.FromArgb(0xff000000);
		VirtualAssetsBuilder.LightOffPalette[246] = Color.FromArgb(0xff000000);
		VirtualAssetsBuilder.LightOffPalette[247] = Color.FromArgb(0xff000000);

		VirtualAssetsBuilder.PlayerPalette = new Color[256];
		VirtualAssetsBuilder.PlayerPalette[248] = Color.FromArgb(0xff660066);
		VirtualAssetsBuilder.PlayerPalette[249] = Color.FromArgb(0xff770077);
		VirtualAssetsBuilder.PlayerPalette[250] = Color.FromArgb(0xff880088);
		VirtualAssetsBuilder.PlayerPalette[251] = Color.FromArgb(0xff990099);
		VirtualAssetsBuilder.PlayerPalette[252] = Color.FromArgb(0xffaa00aa);

		VirtualAssetsBuilder.ShadowsPalette = new Color[256];
		VirtualAssetsBuilder.ShadowsPalette[253] = Color.FromArgb(0x20000000);
		VirtualAssetsBuilder.ShadowsPalette[254] = Color.FromArgb(0x40000000);

		VirtualAssetsBuilder.SmokePalette = new Color[256];

		// TODO find right colors
		for (var i = 0; i < 10; i++)
			VirtualAssetsBuilder.SmokePalette[i + 1] = Color.FromArgb(i * 8, i * 8, i * 8);
	}

	public static Dictionary<string, Stream> BuildAssets(IReadOnlyFileSystem? fileSystem, string name)
	{
		var virtualAssets = new Dictionary<string, Stream>();

		if (fileSystem == null || !fileSystem.TryOpen(name, out var yamlStream))
			return virtualAssets;

		var yaml = MiniYaml.FromStream(yamlStream);

		var source = yaml.FirstOrDefault(e => e.Key == "Source")?.Value.Value;
		var generate = yaml.FirstOrDefault(e => e.Key == "Generate")?.Value;

		if (source == null || generate == null)
			return virtualAssets;

		if (!fileSystem.TryOpen(source, out var stream))
			return virtualAssets;

		// TODO unhardcode .MIX
		var mix = new Mix(stream);

		foreach (var node in generate.Nodes)
			virtualAssets.Add(node.Key + VirtualAssetsBuilder.Extension, new MemoryStream(VirtualAssetsBuilder.BuildSpriteSheet(mix, node)));

		return virtualAssets;
	}

	private static byte[] BuildSpriteSheet(Mix mix, MiniYamlNode sheetNode)
	{
		if (!VirtualAssetsBuilder.Cache.ContainsKey(sheetNode.Key))
		{
			var sheetFlags = sheetNode.Value.Value == null ? Array.Empty<string>() : sheetNode.Value.Value.Split(" ", StringSplitOptions.RemoveEmptyEntries);
			var frames = new List<Frame>();

			foreach (var animationNode in sheetNode.Value.Nodes)
			{
				var chunks = animationNode.Value.Value.Split(" ", StringSplitOptions.RemoveEmptyEntries);

				if (chunks.Length is 0 or > 3)
					throw new Exception("Broken format!");

				var cycles = chunks.Length > 2 ? byte.Parse(chunks[2]) : (byte)1;
				var facings = chunks.Length > 1 ? byte.Parse(chunks[1]) : (byte)1;
				var frameInfos = VirtualAssetsBuilder.BuildFrameInfos(chunks[0], facings);

				foreach (var frameInfo in frameInfos)
				{
					for (var cycle = 0; cycle < cycles; cycle++)
					{
						var mixFrame = mix.Frames[frameInfo.Frame];
						var palette = new Color[256];

						Array.Copy(mix.Palettes[mixFrame.Palette].Colors, 0, palette, 0, palette.Length);

						try
						{
							// TODO move palettes to yaml!
							if (sheetFlags.Contains("Tracks"))
								VirtualAssetsBuilder.ApplyPalette(VirtualAssetsBuilder.TracksPalette, 240, 4, palette, 240, 4, cycle, false);
							else if (sheetFlags.Contains("Rotors"))
								VirtualAssetsBuilder.ApplyPalette(VirtualAssetsBuilder.RotorsPalette, 240, 4, palette, 240, 4, cycle, frameInfo.FlipX);
							else if (sheetFlags.Contains("Engine"))
							{
								VirtualAssetsBuilder.ApplyPalette(
									animationNode.Key == "move" ? VirtualAssetsBuilder.EnginesOnPalette : VirtualAssetsBuilder.EnginesOffPalette,
									240,
									4,
									palette,
									240,
									4,
									cycle,
									false
								);
							}

							if (sheetFlags.Contains("Player"))
								VirtualAssetsBuilder.ApplyPalette(VirtualAssetsBuilder.PlayerPalette, 248, 5, palette, 248, 5, 0, false);

							if (sheetFlags.Contains("Shadow"))
								VirtualAssetsBuilder.ApplyPalette(VirtualAssetsBuilder.ShadowsPalette, 253, 2, palette, 253, 2, 0, false);

							if (sheetFlags.Contains("Smoke"))
								VirtualAssetsBuilder.ApplyPalette(VirtualAssetsBuilder.SmokePalette, 0, 256, palette, 0, 256, 0, false);

							if (sheetFlags.Contains("Muzzle"))
							{
								if (animationNode.Key == "muzzle")
								{
									Array.Fill(palette, Color.FromArgb(0x00000000));

									VirtualAssetsBuilder.ApplyPalette(
										VirtualAssetsBuilder.MuzzlesPalette,
										cycle == 0 ? 247 : 246 - cycle,
										cycle == 0 ? 1 : 3,
										palette,
										244,
										3,
										0,
										false
									);
								}
								else
									Array.Fill(palette, Color.Transparent, 244, 4);
							}
							else if (sheetFlags.Contains("Flicker"))
							{
								if (animationNode.Key == "flicker")
								{
									Array.Fill(palette, Color.Transparent);
									VirtualAssetsBuilder.ApplyPalette(VirtualAssetsBuilder.FlickerOnPalette, 244, 4, palette, 244, 4, -cycle, false);
								}
								else
									VirtualAssetsBuilder.ApplyPalette(VirtualAssetsBuilder.FlickerOffPalette, 244, 4, palette, 244, 4, 0, false);
							}
							else if (sheetFlags.Contains("Light") || sheetFlags.Contains("AddonLight"))
							{
								if (animationNode.Key.EndsWith("_light"))
								{
									Array.Fill(palette, Color.Transparent);
									VirtualAssetsBuilder.ApplyPalette(mix.Palettes[mixFrame.Palette].Colors, 244, 4, palette, 244, 4, 0, false);
								}
								else
									VirtualAssetsBuilder.ApplyPalette(VirtualAssetsBuilder.LightOffPalette, 244, 4, palette, 244, 4, 0, false);
							}

							var frame = new Frame(Rectangle.Empty, Array.Empty<byte>());

							if (sheetFlags.Contains("Infantry"))
								frame = VirtualAssetsBuilder.Draw(frame, mix.Frames[685], VirtualAssetsBuilder.ShadowsPalette, false, new int2(3, 11));
							else if (sheetFlags.Contains("Raptor"))
							{
								// TODO raptor es / ad, uses 686-690
							}

							frames.Add(VirtualAssetsBuilder.Draw(frame, mixFrame, palette, frameInfo.FlipX, new int2(0, 0)));
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

	private static void ApplyPalette(
		IReadOnlyList<Color> source,
		int sourceStart,
		int sourceLength,
		IList<Color> destination,
		int targetStart,
		int targetLength,
		int offset,
		bool flipX
	)
	{
		for (var i = 0; i < targetLength; i++)
			destination[targetStart + i] = source[sourceStart + ((flipX ? targetLength - i - 1 : i) + offset) % sourceLength];
	}

	private static List<FrameInfo> BuildFrameInfos(string frames, byte facings)
	{
		var frameInfos = new List<FrameInfo>();

		foreach (var segment in frames.Split(','))
		{
			var match = Regex.Match(segment, "^(\\d+)(?:-(\\d+)(?:\\[(\\d+):(\\d+):(\\d+)\\])?)?$");

			if (!match.Success || !match.Groups[1].Success)
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
					frameInfos.Add(new FrameInfo(frame, false));
			}
		}

		if (facings <= 1)
			return frameInfos;

		var framesPerFacing = frameInfos.Count / (facings / 2 + 1);

		for (var facing = facings / 2 - 1; facing > 0; facing--)
		for (var j = 0; j < framesPerFacing; j++)
			frameInfos.Add(new FrameInfo(frameInfos[facing * framesPerFacing + j].Frame, true));

		return frameInfos;
	}

	private static Frame Draw(Frame frame, MixFrame mixFrame, IReadOnlyList<Color> palette, bool flipX, int2 offset)
	{
		var usedBounds = new Rectangle(mixFrame.Width, mixFrame.Height, 0, 0);
		var pixels = new byte[mixFrame.Width * mixFrame.Height * 4];

		for (var x = 0; x < mixFrame.Width; x++)
		for (var y = 0; y < mixFrame.Height; y++)
		{
			var color = palette[mixFrame.Pixels[y * mixFrame.Width + (flipX ? mixFrame.Width - x - 1 : x)]];

			if (color.A == 0)
				continue;

			var writeOffset = y * mixFrame.Width + x;

			pixels[writeOffset * 4 + 0] = color.R;
			pixels[writeOffset * 4 + 1] = color.G;
			pixels[writeOffset * 4 + 2] = color.B;
			pixels[writeOffset * 4 + 3] = color.A;

			if (x < usedBounds.Left)
				usedBounds.X = x;

			if (y < usedBounds.Top)
				usedBounds.Y = y;

			if (x >= usedBounds.Right)
				usedBounds.Width = x - usedBounds.Left + 1;

			if (y >= usedBounds.Bottom)
				usedBounds.Height = y - usedBounds.Top + 1;
		}

		// TODO fix me!
		usedBounds = new Rectangle(0, 0, mixFrame.Width, mixFrame.Height);

		var bounds = new Rectangle(
			usedBounds.Left - mixFrame.Width / 2 + offset.X,
			usedBounds.Top - mixFrame.Height / 2 + offset.Y,
			usedBounds.Width,
			usedBounds.Height
		);

		if (!frame.Bounds.Contains(bounds))
		{
			var newBounds = new Rectangle(
				Math.Min(frame.Bounds.Left, bounds.Left),
				Math.Min(frame.Bounds.Top, bounds.Top),
				Math.Max(frame.Bounds.Right, bounds.Right) - Math.Min(frame.Bounds.Left, bounds.Left),
				Math.Max(frame.Bounds.Bottom, bounds.Bottom) - Math.Min(frame.Bounds.Top, bounds.Top)
			);

			var newPixels = new byte[newBounds.Width * newBounds.Height * 4];

			for (var y = 0; y < frame.Bounds.Height; y++)
			{
				Array.Copy(
					frame.Pixels,
					y * frame.Bounds.Width * 4,
					newPixels,
					((y - newBounds.Top + frame.Bounds.Top) * newBounds.Width - newBounds.Left + frame.Bounds.Left) * 4,
					frame.Bounds.Width * 4
				);
			}

			frame = new Frame(newBounds, newPixels);
		}

		for (var x = 0; x < usedBounds.Width; x++)
		for (var y = 0; y < usedBounds.Height; y++)
		{
			var readOffset = ((usedBounds.Top + y) * mixFrame.Width + usedBounds.Left + x) * 4;

			if (pixels[readOffset + 3] != 0)
			{
				Array.Copy(
					pixels,
					readOffset,
					frame.Pixels,
					((y - frame.Bounds.Top + bounds.Top) * frame.Bounds.Width + x - frame.Bounds.Left + bounds.Left) * 4,
					4
				);
			}
		}

		return frame;
	}
}
