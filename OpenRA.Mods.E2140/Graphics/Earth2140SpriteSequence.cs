#region Copyright & License Information

/*
 * Copyright 2007-2023 The Earth 2140 Developers (see AUTHORS)
 * This file is part of Earth 2140, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */

#endregion

using JetBrains.Annotations;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;

namespace OpenRA.Mods.E2140.Graphics;

[UsedImplicitly]
public class Earth2140SpriteSequenceLoader : DefaultSpriteSequenceLoader
{
	[Desc("Dictionary of <string: string> with tileset name to override -> sprite name to use instead.")]
	public readonly Dictionary<string, string> TilesetOverrides = new Dictionary<string, string>();

	public Earth2140SpriteSequenceLoader(ModData modData)
		: base(modData)
	{
		var metadata = modData.Manifest.Get<SpriteSequenceFormat>().Metadata;
		if (metadata.TryGetValue("TilesetOverrides", out var yaml))
			this.TilesetOverrides = yaml.ToDictionary(kv => kv.Value);
	}

	public override ISpriteSequence CreateSequence(ModData modData, string tileSet, SpriteCache cache, string sequence, string animation, MiniYaml info)
	{
		return new Earth2140SpriteSequence(modData, tileSet, cache, this, sequence, animation, info);
	}
}

public class Earth2140SpriteSequence : DefaultSpriteSequence
{
	public Earth2140SpriteSequence(
		ModData modData,
		string tileSet,
		SpriteCache cache,
		ISpriteSequenceLoader loader,
		string sequence,
		string animation,
		MiniYaml info
	)
		: base(modData, tileSet, cache, loader, sequence, animation, Earth2140SpriteSequence.Preprocess(tileSet, loader, info))
	{
	}

	private static MiniYaml Preprocess(string tileSet, ISpriteSequenceLoader loader, MiniYaml info)
	{
		var e2140Loader = (Earth2140SpriteSequenceLoader)loader;

		var earthFormatNode = info.Nodes.FirstOrDefault(node => node.Key == "EarthFormat");

		if (earthFormatNode != null)
		{
			return Earth2140SpriteSequence.FlipFacings(info);
		}

		var tilesetSpecific = info.Nodes.FirstOrDefault(node => node.Key == "TilesetSpecific");
		if (tilesetSpecific != null)
		{
			return Earth2140SpriteSequence.ChangeSpritesForTileset(tileSet, e2140Loader, info);
		}

		return info;
	}

	private static MiniYaml FlipFacings(MiniYaml info)
	{
		var settings = info.ToDictionary();

		var facings = Earth2140SpriteSequence.GetInt(settings, "Facings", 1);
		var start = Earth2140SpriteSequence.GetInt(settings, "Start", 0);
		var length = Earth2140SpriteSequence.GetInt(settings, "Length", 1);
		var stride = Earth2140SpriteSequence.GetInt(settings, "Stride", 1);
		var reverse = Earth2140SpriteSequence.GetBool(settings, "Reverse", false);

		var combineNode = new MiniYamlNode("Combine", "");

		for (var facing = 0; facing < facings; facing++)
		{
			var facingNode = new MiniYamlNode(info.Value, "");
			facingNode.Value.Nodes.Add(new MiniYamlNode("Length", $"{length}"));

			var frames = Enumerable.Range(0, length).Select(i => start + ((facing > facings / 2 ? facings - facing : facing) * stride + i) * 2).ToArray();

			if (reverse)
				frames = frames.Reverse().ToArray();

			facingNode.Value.Nodes.Add(new MiniYamlNode("Frames", string.Join(',', frames)));

			if (facing > facings / 2)
				facingNode.Value.Nodes.Add(new MiniYamlNode("FlipX", "true"));

			combineNode.Value.Nodes.Add(facingNode);
		}

		var newInfo = new MiniYaml("");
		newInfo.Nodes.Add(new MiniYamlNode("Length", $"{length}"));
		newInfo.Nodes.Add(new MiniYamlNode("Facings", $"{-facings}"));
		newInfo.Nodes.Add(combineNode);

		return newInfo;
	}

	private static MiniYaml ChangeSpritesForTileset(string tileSet, Earth2140SpriteSequenceLoader loader, MiniYaml info)
	{
		if (!loader.TilesetOverrides.TryGetValue(tileSet, out var spriteName))
			throw new Exception($"Unknown tileset '{tileSet}', cannot determine sprite name");

		return new MiniYaml(spriteName, info.Nodes);
	}

	private static bool GetBool(IReadOnlyDictionary<string, MiniYaml> settings, string key, bool fallback)
	{
		return !settings.TryGetValue(key, out var value) ? fallback :
			!bool.TryParse(value.Value, out var intValue) ? fallback : intValue;
	}

	private static int GetInt(IReadOnlyDictionary<string, MiniYaml> settings, string key, int fallback)
	{
		return !settings.TryGetValue(key, out var value) ? fallback :
			!int.TryParse(value.Value, out var intValue) ? fallback : intValue;
	}
}
