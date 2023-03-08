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

using System.Text.RegularExpressions;
using OpenRA.FileSystem;

namespace OpenRA.Mods.OpenE2140.Assets.Audio;

public class AudioClassifier
{
	private readonly AudioClassifications audioClassifications;

	public AudioClassifier(IReadOnlyFileSystem fileSystem)
	{
		if (fileSystem is null)
			throw new ArgumentNullException(nameof(fileSystem));

		var yamlConfig = MiniYaml.FromStream(fileSystem.Open($"content/core/audio/audio_classifications.yaml")).First().Value;
		this.audioClassifications = new AudioClassifications(yamlConfig);
	}

	public string ClassifyFilename(string wdFilename, string audioFilename)
	{
		if (!this.audioClassifications.FactionVariantSounds.Contains(audioFilename))
			return audioFilename;

		if (!this.audioClassifications.SoundFilePrefixes.TryGetValue(wdFilename, out var prefix))
			throw new InvalidOperationException($"Unknown WD file {wdFilename}, cannot classify sound {audioFilename}");

		return $"{prefix}{audioFilename}";
	}

	private class AudioClassifications
	{
		public readonly Dictionary<string, string> SoundFilePrefixes = new Dictionary<string, string>();
		public readonly HashSet<string> FactionVariantSounds = new HashSet<string>();

		public AudioClassifications(MiniYaml yaml)
		{
			FieldLoader.Load(this, yaml);
			this.FactionVariantSounds = AudioClassifications.ExpandFactionVariantSounds(this.FactionVariantSounds).ToHashSet();
		}

		private static IEnumerable<string> ExpandFactionVariantSounds(IEnumerable<string> factionVariantSounds)
		{
			foreach (var item in factionVariantSounds.SelectMany(l => l.Split(",")))
			{
				var match = Regex.Match(item, "^(\\d+)(?:-(\\d+)?)?$");

				if (!match.Success || !match.Groups[1].Success)
					throw new InvalidOperationException($"Invalid value for FactionVariantSounds: {item}!");

				var firstSound = int.Parse(match.Groups[1].Value);
				var lastSound = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : firstSound;

				for (var i = firstSound; i <= lastSound; i++)
					yield return $"{i}.smp";
			}
		}
	}
}
