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

using JetBrains.Annotations;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Render;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("Custom trait that generates FactionImages from actor name")]
public class FactionRenderSpritesInfo : TraitInfo
{
	[Desc(
		"List of factions to generate faction images for. Faction image is not generated for faction, which name is prefix of actor's name"
		+ "(e.g. 'ucs_vehicles_tiger_assault' is UCS unit by default, so it's considered as default.)"
	)]
	public readonly List<string> Factions = [];

	public override object Create(ActorInitializer init)
	{
		return new FactionRenderSprites(this);
	}
}

public class FactionRenderSprites : IWorldLoaded
{
	private readonly FactionRenderSpritesInfo info;

	public FactionRenderSprites(FactionRenderSpritesInfo info)
	{
		this.info = info;
	}

	void IWorldLoaded.WorldLoaded(World world, WorldRenderer worldRenderer)
	{
		foreach (var actorInfo in world.Map.Rules.Actors.Values)
		{
			var renderSpritesInfo = actorInfo.TraitInfoOrDefault<RenderSpritesInfo>();

			if (renderSpritesInfo == null)
				continue;

			foreach (var faction in this.info.Factions)
			{
				var factionImageName = $"{actorInfo.Name}.{faction}";

				if (world.Map.Sequences.Images.Contains(factionImageName))
					renderSpritesInfo.FactionImages.TryAdd(faction, factionImageName);
			}
		}
	}
}
