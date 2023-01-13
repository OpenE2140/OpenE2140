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

using JetBrains.Annotations;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.E2140.Assets.VirtualAssets;

namespace OpenRA.Mods.E2140.Graphics;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class OpenE2140SpriteSequenceLoader : DefaultSpriteSequenceLoader, ISpriteSequenceLoader
{
	[Desc("Dictionary of <string: string> with tileset name to override -> sprite name to use instead.")]
	public readonly Dictionary<string, string> TilesetOverrides = new Dictionary<string, string>();

	public OpenE2140SpriteSequenceLoader(ModData modData)
		: base(modData)
	{
		var metadata = modData.Manifest.Get<SpriteSequenceFormat>().Metadata;

		if (metadata.TryGetValue("TilesetOverrides", out var yaml))
			this.TilesetOverrides = yaml.ToDictionary(kv => kv.Value);
	}

	public new IReadOnlyDictionary<string, ISpriteSequence> ParseSequences(ModData modData, string tileSet, SpriteCache cache, MiniYamlNode node)
	{
		// Virtual assets implementation
		VirtualAssetsBuilder.BuildSequences(node);

		return base.ParseSequences(modData, tileSet, cache, node);
	}

	public override ISpriteSequence CreateSequence(ModData modData, string tileSet, SpriteCache cache, string sequence, string animation, MiniYaml info)
	{
		var tilesetSpecific = info.Nodes.FirstOrDefault(node => node.Key == "TilesetSpecific");

		if (tilesetSpecific != null)
			info = OpenE2140SpriteSequenceLoader.ChangeSpritesForTileset(tileSet, this, info);

		return new DefaultSpriteSequence(modData, tileSet, cache, this, sequence, animation, info);
	}

	private static MiniYaml ChangeSpritesForTileset(string tileSet, OpenE2140SpriteSequenceLoader loader, MiniYaml info)
	{
		if (!loader.TilesetOverrides.TryGetValue(tileSet, out var spriteName))
			throw new Exception($"Unknown tileset '{tileSet}', cannot determine sprite name");

		return new MiniYaml(spriteName, info.Nodes);
	}
}
