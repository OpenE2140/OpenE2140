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

namespace OpenRA.Mods.OpenE2140.Assets.Extractors;

public static class AudioExtractor
{
	public static void Extract(ISoundFormat sound, string name)
	{
		var outputFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "OpenE2140Extracted", $"{name}.wav");

		Directory.CreateDirectory(Path.GetDirectoryName(outputFile) ?? "");

		var stream = sound.GetPCMInputStream();

		var data = stream.ReadAllBytes();
		var dataSize = data.Length;

		var channels = (ushort)sound.Channels;
		var sampleBits = (ushort)sound.SampleBits;
		var sampleRate = (uint)sound.SampleRate;

		var format = (ushort)1;
		var fmtSize = 16;

		var blockAlign = (ushort)(channels * ((sampleBits + 7) / 8));
		var bytesPerSecond = sound.SampleRate * blockAlign;

		using var output = new MemoryStream();
		var writer = new BinaryWriter(output);

		writer.Write(Encoding.ASCII.GetBytes("RIFF"));
		writer.Write(4 + 8 + fmtSize + 8 + dataSize);

		{
			writer.Write(Encoding.ASCII.GetBytes("WAVE"));
			writer.Write(Encoding.ASCII.GetBytes("fmt "));
			writer.Write(fmtSize);

			{
				writer.Write(format);
				writer.Write(channels);
				writer.Write(sampleRate);
				writer.Write(bytesPerSecond);
				writer.Write(blockAlign);
				writer.Write(sampleBits);
			}

			writer.Write(Encoding.ASCII.GetBytes("data"));
			writer.Write(dataSize);

			{
				writer.Write(data);
			}
		}

		File.WriteAllBytes(outputFile, output.ToArray());
	}
}
