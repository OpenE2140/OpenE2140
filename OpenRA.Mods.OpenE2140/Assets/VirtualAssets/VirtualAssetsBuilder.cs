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
using OpenRA.Mods.E2140.Assets.FileFormats;
using OpenRA.Primitives;

namespace OpenRA.Mods.E2140.Assets.VirtualAssets;

public static class VirtualAssetsBuilder
{
	public const string Identifier = "VirtualSpriteSheet";
	public const string Extension = ".vspr";
	public static readonly Dictionary<string, VirtualSpriteSheet> Cache = new Dictionary<string, VirtualSpriteSheet>();

	private record FrameInfo(int Frame, bool FlipX);

	private record SequenceInfo(string Name, int Length);

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

				if (animationNode.Key == "idle" && (sheetFlags.Contains("Tracks") || sheetFlags.Contains("Engine") || sheetFlags.Contains("Rotors")))
					sequences.Add(new SequenceInfo("move", 4));

				if (animationNode.Key == "idle" && (sheetFlags.Contains("Attack") || sheetFlags.Contains("Light")))
					sequences.Add(new SequenceInfo("effect", 4));

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

							if (sheetFlags.Contains("Tracks") || sheetFlags.Contains("Rotors"))
							{
								// TODO all colors are guessed!
								palette[240 + (4 - i) % 4] = Color.FromArgb(0xff181c18);
								palette[240 + (5 - i) % 4] = Color.FromArgb(0xff212421);
								palette[240 + (6 - i) % 4] = Color.FromArgb(0xff181c18);
								palette[240 + (7 - i) % 4] = Color.FromArgb(0xff292c29);
							}
							else if (sheetFlags.Contains("Engine"))
							{
								// TODO all colors are guessed!
								palette[240 + (4 - i) % 4] = Color.FromArgb(0xffff9e52);
								palette[240 + (5 - i) % 4] = Color.FromArgb(0xffefb68c);
								palette[240 + (6 - i) % 4] = Color.FromArgb(0xffffebc6);
								palette[240 + (7 - i) % 4] = Color.FromArgb(0xffffffff);
							}

							if (sheetFlags.Contains("Player"))
							{
								// TODO all colors are guessed!
								palette[248] = Color.FromArgb(0xff660066);
								palette[249] = Color.FromArgb(0xff770077);
								palette[250] = Color.FromArgb(0xff880088);
								palette[251] = Color.FromArgb(0xff990099);
								palette[252] = Color.FromArgb(0xffaa00aa);
							}

							if (sheetFlags.Contains("Shadow"))
							{
								// TODO all colors are guessed!
								palette[253] = Color.FromArgb(0x40000000);
								palette[254] = Color.FromArgb(0x80000000);
							}

							if (sheetFlags.Contains("Attack"))
							{
								if (sequenceInfo.Name == "effect")
								{
									Array.Fill(palette, Color.FromArgb(0x00000000));

									// TODO all colors are guessed!
									palette[244] = Color.FromArgb(0xffff9e52);
									palette[245] = Color.FromArgb(0xffefb68c);
									palette[246] = Color.FromArgb(0xffffebc6);
									palette[247] = Color.FromArgb(0xffffffff);

									for (var j = 0; j < i; j++)
									{
										palette[247] = palette[246];
										palette[246] = palette[245];
										palette[245] = palette[244];
										palette[244] = palette[247];
										palette[244] = Color.FromArgb(0x00000000);
									}
								}
								else
								{
									palette[244] = Color.FromArgb(0x00000000);
									palette[245] = Color.FromArgb(0x00000000);
									palette[246] = Color.FromArgb(0x00000000);
									palette[247] = Color.FromArgb(0x00000000);
								}
							}
							else if (sheetFlags.Contains("Light"))
							{
								if (sequenceInfo.Name == "effect")
								{
									Array.Fill(palette, Color.FromArgb(0x00000000));

									// TODO all colors are guessed!
									palette[244 + (4 - i) % 4] = Color.FromArgb(0xffff9e52); // TODO colors!
									palette[244 + (5 - i) % 4] = Color.FromArgb(0xffefb68c); // TODO colors!
									palette[244 + (6 - i) % 4] = Color.FromArgb(0xffffebc6); // TODO colors!
									palette[244 + (7 - i) % 4] = Color.FromArgb(0xffffffff); // TODO colors!
								}
								else
								{
									palette[244] = Color.FromArgb(0xff000000);
									palette[245] = Color.FromArgb(0xff111111);
									palette[246] = Color.FromArgb(0xff222222);
									palette[247] = Color.FromArgb(0xff333333);
								}
							}

							frames.Add(VirtualAssetsBuilder.BuildFrames(mixFrame, palette, frameInfo.FlipX));
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

		return new VirtualSpriteFrame(mixFrame.Width, mixFrame.Height, pixels);
	}
}
