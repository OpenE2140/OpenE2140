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

using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Rendering;

[Desc("Custom trait based on RenderSprites that generates FactionImages from actor name")]
public class FactionRenderSpritesInfo : RenderSpritesInfo, IRulesetLoaded
{
	[Desc(
		"List of factions to generate faction images for. Faction image is not generated for faction, which name is prefix of actor's name"
		+ "(e.g. 'ucs_vehicles_tiger_assault' is UCS unit by default, so it's considered as default.)"
	)]
	public readonly List<string> Factions = new List<string>();

	public void RulesetLoaded(Ruleset rules, ActorInfo info)
	{
		if (this.FactionImages == null)
			throw new YamlException("Please initialize FactionImages");

		var existingFactions = rules.Actors[SystemActors.World].TraitInfos<FactionInfo>().Select(f => f.InternalName);
		var unknownFactions = this.Factions.Where(f => !existingFactions.Contains(f)).ToArray();

		if (unknownFactions.Any())
			throw new YamlException($"Unknown factions: {string.Join(", ", unknownFactions)}");

		if (rules.Sequences == null)
			return;

		foreach (var faction in this.Factions)
		{
			if (info.Name.StartsWith(faction))
				continue;

			if (this.FactionImages.ContainsKey(faction))
				continue;

			var factionImageName = $"{info.Name}.{faction}";

			if (!rules.Sequences.HasSequence(factionImageName))
				continue;

			this.FactionImages.TryAdd(faction, factionImageName);
		}
	}
}
