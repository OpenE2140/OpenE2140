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
using OpenRA.Mods.OpenE2140.Assets.VirtualAssets;

namespace OpenRA.Mods.OpenE2140.Graphics;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class OpenE2140SpriteSequenceLoader : TilesetSpecificSpriteSequenceLoader, ISpriteSequenceLoader
{
	public OpenE2140SpriteSequenceLoader(ModData modData)
		: base(modData)
	{
	}

	public new IReadOnlyDictionary<string, ISpriteSequence> ParseSequences(ModData modData, string tileSet, SpriteCache cache, MiniYamlNode node)
	{
		// Virtual assets implementation
		VirtualAssetsBuilder.BuildSequences(node);

		return base.ParseSequences(modData, tileSet, cache, node);
	}

	public override ISpriteSequence CreateSequence(ModData modData, string tileset, SpriteCache cache, string image, string sequence, MiniYaml data, MiniYaml defaults)
	{
		return new TilesetSpecificSpriteSequence(modData, tileset, cache, this, image, sequence, data, defaults);
	}
}
