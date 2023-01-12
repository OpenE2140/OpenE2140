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
using OpenRA.Mods.E2140.Assets.FileFormats;
using OpenRA.Primitives;

namespace OpenRA.Mods.E2140.Assets.VirtualAssets;

public static class VMixBuilder
{
	private record FrameInfo(int Frame, bool FlipX);

	private record SequenceInfo(string Name, int Length);

	public static byte[] Build(Mix mix, MiniYamlNode sheetNode)
	{
		if (!VMix.Cache.ContainsKey(sheetNode.Key))
		{
			var sheetFlags = sheetNode.Value.Value == null ? Array.Empty<string>() : sheetNode.Value.Value.Split(" ", StringSplitOptions.RemoveEmptyEntries);
			var vMixAnimations = new List<VMixAnimation>();

			foreach (var animationNode in sheetNode.Value.Nodes)
			{
				var chunks = animationNode.Value.Value.Split(" ", StringSplitOptions.RemoveEmptyEntries);

				if (chunks.Length is 0 or > 2)
					throw new Exception("Broken format!");

				var sequences = new List<SequenceInfo> { new SequenceInfo(animationNode.Key, 1) };

				if (animationNode.Key == "idle" && (sheetFlags.Contains("Tracks") || sheetFlags.Contains("Engine") || sheetFlags.Contains("Rotors")))
					sequences.Add(new SequenceInfo("move", 4));

				foreach (var sequenceInfo in sequences)
				{
					var facings = chunks.Length > 1 ? byte.Parse(chunks[1]) : (byte)1;
					var frameInfos = VMixBuilder.BuildFrameInfos(chunks[0], facings);

					var vMixFrames = new List<VMixFrame>();

					foreach (var frameInfo in frameInfos)
					{
						for (var i = 0; i < sequenceInfo.Length; i++)
						{
							var mixFrame = mix.Frames[frameInfo.Frame];
							var palette = new Color[256];

							Array.Copy(mix.Palettes[mixFrame.Palette].Colors, 0, palette, 0, palette.Length);

							if (sheetFlags.Contains("Tracks") || sheetFlags.Contains("Engine") || sheetFlags.Contains("Rotors"))
							{
								palette[240 + (4 - i) % 4] = Color.FromArgb(0xff990000); // TODO colors!
								palette[240 + (5 - i) % 4] = Color.FromArgb(0xffbb0000); // TODO colors!
								palette[240 + (6 - i) % 4] = Color.FromArgb(0xffdd0000); // TODO colors!
								palette[240 + (7 - i) % 4] = Color.FromArgb(0xffff0000); // TODO colors!
							}

							if (true) // TODO Attack (attack ? visible : invisible) or Light (attack ? visible : black)
							{
								palette[244] = Color.FromArgb(0xff009900); // TODO colors!
								palette[245] = Color.FromArgb(0xff00bb00); // TODO colors!
								palette[246] = Color.FromArgb(0xff00dd00); // TODO colors!
								palette[247] = Color.FromArgb(0xff00ff00); // TODO colors!
							}

							if (sheetFlags.Contains("Player"))
							{
								palette[248] = Color.FromArgb(0xff660066);
								palette[249] = Color.FromArgb(0xff770077);
								palette[250] = Color.FromArgb(0xff880088);
								palette[251] = Color.FromArgb(0xff990099);
								palette[252] = Color.FromArgb(0xffaa00aa);
							}

							if (sheetFlags.Contains("Shadow"))
							{
								palette[253] = Color.FromArgb(0x40000000);
								palette[254] = Color.FromArgb(0x80000000);
							}

							vMixFrames.Add(VMixBuilder.BuildFrames(mixFrame, palette, frameInfo.FlipX));
						}
					}

					vMixAnimations.Add(new VMixAnimation(sequenceInfo.Name, facings, vMixFrames.ToArray()));
				}
			}

			VMix.Cache.Add(sheetNode.Key, new VMix(vMixAnimations.ToArray()));
		}

		var stream = new MemoryStream();
		var writer = new BinaryWriter(stream);
		writer.Write(Encoding.ASCII.GetBytes("VMIX"));
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

	private static VMixFrame BuildFrames(MixFrame mixFrame, IReadOnlyList<Color> palette, bool flipX)
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

		return new VMixFrame(mixFrame.Width, mixFrame.Height, pixels);
	}
}
