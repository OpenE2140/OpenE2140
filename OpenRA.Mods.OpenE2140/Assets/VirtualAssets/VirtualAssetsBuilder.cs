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
using OpenRA.Mods.Common.UpdateRules;
using OpenRA.Mods.OpenE2140.Assets.FileFormats;
using OpenRA.Primitives;

namespace OpenRA.Mods.OpenE2140.Assets.VirtualAssets;

public static class VirtualAssetsBuilder
{
	public const string Identifier = "VirtualSpriteSheet";
	private const string Extension = ".vspr";
	public static readonly Dictionary<string, VirtualSpriteSheet> Cache = new Dictionary<string, VirtualSpriteSheet>();

	private record FrameInfo(int Frame, bool FlipX);

	private record SequenceInfo(string Name, int Length);

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
		VirtualAssetsBuilder.ShadowsPalette[253] = Color.FromArgb(0x40000000);
		VirtualAssetsBuilder.ShadowsPalette[254] = Color.FromArgb(0x80000000);
	}

	public static Dictionary<string, Stream> BuildAssets(IReadOnlyFileSystem? fileSystem, string name, IReadOnlyPackage package)
	{
		var virtualAssets = new Dictionary<string, Stream>();

		if (fileSystem == null || !fileSystem.TryOpen($"virtualassets/{name}.yaml", out var yamlStream))
			return virtualAssets;

		if (name.EndsWith(".mix", StringComparison.OrdinalIgnoreCase))
		{
			var mix = new Mix(package.GetStream(name));

			foreach (var node in MiniYaml.FromStream(yamlStream))
				virtualAssets.Add(node.Key + VirtualAssetsBuilder.Extension, new MemoryStream(VirtualAssetsBuilder.BuildSpriteSheet(mix, node)));
		}
		else
			throw new Exception("Not supported!");

		return virtualAssets;
	}

	public static void BuildSequences(MiniYamlNode node)
	{
		if (node.Value.Value == null || !node.Value.Value.EndsWith(VirtualAssetsBuilder.Extension))
			return;

		var spriteSheet = VirtualAssetsBuilder.Cache[node.Value.Value[..^VirtualAssetsBuilder.Extension.Length]];

		var offset = 0;

		foreach (var animation in spriteSheet.Animations)
		{
			var sequenceNode = node.Value.Nodes.FirstOrDefault(n => n.Key == animation.Name);

			if (sequenceNode == null)
				node.Value.Nodes.Add(sequenceNode = new MiniYamlNode(animation.Name, node.Value.Value));
			else if (sequenceNode.Value.Value == null)
				sequenceNode.Value.Value = node.Value.Value;

			if (sequenceNode.Value.Nodes.All(n => n.Key != "Start"))
				sequenceNode.AddNode("Start", offset);

			if (sequenceNode.Value.Nodes.All(n => n.Key != "Length"))
				sequenceNode.AddNode("Length", animation.Frames.Length / animation.Facings);

			if (sequenceNode.Value.Nodes.All(n => n.Key != "Facings"))
				sequenceNode.AddNode("Facings", -animation.Facings);

			offset += animation.Frames.Length;
		}
	}

	private static byte[] BuildSpriteSheet(Mix mix, MiniYamlNode sheetNode)
	{
		if (!VirtualAssetsBuilder.Cache.ContainsKey(sheetNode.Key))
		{
			var sheetFlags = sheetNode.Value.Value == null ? Array.Empty<string>() : sheetNode.Value.Value.Split(" ", StringSplitOptions.RemoveEmptyEntries);
			var animations = new List<VirtualSpriteAnimation>();

			foreach (var animationNode in sheetNode.Value.Nodes)
			{
				var chunks = animationNode.Value.Value.Split(" ", StringSplitOptions.RemoveEmptyEntries);

				if (chunks.Length is 0 or > 2)
					throw new Exception("Broken format!");

				var sequences = new List<SequenceInfo> { new SequenceInfo(animationNode.Key, 1) };

				if (animationNode.Key.StartsWith("idle"))
				{
					if (sheetFlags.Contains("Tracks") || sheetFlags.Contains("Rotors"))
						sequences.Add(new SequenceInfo("move", 4));
					else if (sheetFlags.Contains("Engine"))
						sequences.Add(new SequenceInfo("move", 1));

					if (sheetFlags.Contains("Muzzle"))
						sequences.Add(new SequenceInfo("muzzle", 3));
					else if (sheetFlags.Contains("Flicker"))
						sequences.Add(new SequenceInfo("flicker", 2));

					if (sheetFlags.Contains("Light"))
						sequences.Add(new SequenceInfo($"{animationNode.Key}_light", 1));
				}
				else if (animationNode.Key.StartsWith("addon") && sheetFlags.Contains("AddonLight"))
					sequences.Add(new SequenceInfo($"{animationNode.Key}_light", 1));

				foreach (var sequenceInfo in sequences)
				{
					var facings = chunks.Length > 1 ? byte.Parse(chunks[1]) : (byte)1;
					var frameInfos = VirtualAssetsBuilder.BuildFrameInfos(chunks[0], facings);

					var frames = new List<VirtualSpriteFrame>();

					foreach (var frameInfo in frameInfos)
					{
						for (var i = 0; i < sequenceInfo.Length; i++)
						{
							var mixFrame = mix.Frames[frameInfo.Frame];
							var palette = new Color[256];

							Array.Copy(mix.Palettes[mixFrame.Palette].Colors, 0, palette, 0, palette.Length);

							try
							{
								if (sheetFlags.Contains("Tracks"))
									VirtualAssetsBuilder.ApplyPalette(VirtualAssetsBuilder.TracksPalette, 240, 4, palette, 240, 4, i);
								else if (sheetFlags.Contains("Rotors"))
									VirtualAssetsBuilder.ApplyPalette(VirtualAssetsBuilder.RotorsPalette, 240, 4, palette, 240, 4, i);
								else if (sheetFlags.Contains("Engine"))
								{
									VirtualAssetsBuilder.ApplyPalette(
										sequenceInfo.Name == "move" ? VirtualAssetsBuilder.EnginesOnPalette : VirtualAssetsBuilder.EnginesOffPalette,
										240,
										4,
										palette,
										240,
										4,
										i
									);
								}

								if (sheetFlags.Contains("Player"))
									VirtualAssetsBuilder.ApplyPalette(VirtualAssetsBuilder.PlayerPalette, 248, 5, palette, 248, 5, 0);

								if (sheetFlags.Contains("Shadow"))
									VirtualAssetsBuilder.ApplyPalette(VirtualAssetsBuilder.ShadowsPalette, 253, 2, palette, 253, 2, 0);

								if (sheetFlags.Contains("Attack"))
								{
									if (sequenceInfo.Name == "muzzle")
									{
										Array.Fill(palette, Color.FromArgb(0x00000000));

										VirtualAssetsBuilder.ApplyPalette(
											VirtualAssetsBuilder.MuzzlesPalette,
											i == 0 ? 247 : 246 - i,
											i == 0 ? 1 : 3,
											palette,
											244,
											3,
											0
										);
									}
									else
										Array.Fill(palette, Color.Transparent, 244, 4);
								}
								else if (sheetFlags.Contains("Flicker"))
								{
									if (sequenceInfo.Name == "flicker")
									{
										Array.Fill(palette, Color.Transparent);
										VirtualAssetsBuilder.ApplyPalette(VirtualAssetsBuilder.FlickerOnPalette, 244, 4, palette, 244, 4, -i);
									}
									else
										VirtualAssetsBuilder.ApplyPalette(VirtualAssetsBuilder.FlickerOffPalette, 244, 4, palette, 244, 4, 0);
								}
								else if (sheetFlags.Contains("Light") || sheetFlags.Contains("AddonLight"))
								{
									if (sequenceInfo.Name.EndsWith("_light"))
									{
										Array.Fill(palette, Color.Transparent);
										VirtualAssetsBuilder.ApplyPalette(mix.Palettes[mixFrame.Palette].Colors, 244, 4, palette, 244, 4, 0);
									}
									else
										VirtualAssetsBuilder.ApplyPalette(VirtualAssetsBuilder.LightOffPalette, 244, 4, palette, 244, 4, 0);
								}
							}
							catch (Exception e)
							{
								Console.WriteLine(e);
							}

							// TODO we should make an empty frame first, and build a "Draw" function, which draws on top. Required for the shadows. Should auto-trim!
							var frame = VirtualAssetsBuilder.BuildFrames(mixFrame, palette, frameInfo.FlipX);

							if (sheetFlags.Contains("Infantry"))
							{
								var shadow = VirtualAssetsBuilder.BuildFrames(mix.Frames[685], VirtualAssetsBuilder.ShadowsPalette, false);
								var shadowOffsetX = (frame.Width - shadow.Width) / 2;
								var shadowOffsetY = (frame.Height - shadow.Height) / 2;

								for (var x = 0; x < shadow.Width; x++)
								for (var y = 0; y < shadow.Height; y++)
								{
									var target = ((shadowOffsetY + y) * frame.Width + shadowOffsetX + x) * 4;

									if (frame.Pixels[target + 3] == 0)
										frame.Pixels[target + 3] = shadow.Pixels[(y * shadow.Width + x) * 4 + 3];
								}
							}

							frames.Add(frame);
						}
					}

					animations.Add(new VirtualSpriteAnimation(sequenceInfo.Name, facings, frames.ToArray()));
				}
			}

			VirtualAssetsBuilder.Cache.Add(sheetNode.Key, new VirtualSpriteSheet(animations.ToArray()));
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
		int offset
	)
	{
		for (var i = 0; i < targetLength; i++)
			destination[targetStart + i] = source[sourceStart + (i + offset) % sourceLength];
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

	private static VirtualSpriteFrame BuildFrames(MixFrame mixFrame, IReadOnlyList<Color> palette, bool flipX)
	{
		var pixels = new byte[mixFrame.Width * mixFrame.Height * 4];

		for (var i = 0; i < pixels.Length / 4; i++)
		{
			var index = mixFrame.Pixels[flipX ? i / mixFrame.Width * mixFrame.Width + (mixFrame.Width - i % mixFrame.Width - 1) : i];
			var color = palette[index];

			pixels[i * 4 + 0] = color.R;
			pixels[i * 4 + 1] = color.G;
			pixels[i * 4 + 2] = color.B;
			pixels[i * 4 + 3] = color.A;
		}

		return new VirtualSpriteFrame(mixFrame.Width, mixFrame.Height, new float2(0, 0), pixels);
	}
}
