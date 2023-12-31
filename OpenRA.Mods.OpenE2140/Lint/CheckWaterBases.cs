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

using OpenRA.Mods.Common.Lint;
using OpenRA.Mods.OpenE2140.Traits.WaterBase;

namespace OpenRA.Mods.OpenE2140.Lint;

public class CheckWaterBases : ILintMapPass
{
	private static readonly string WaterBaseDockInitName = nameof(WaterBaseDockInit)[..^"Init".Length];

	public void Run(Action<string> emitError, Action<string> emitWarning, ModData modData, Map map)
	{
		var waterBaseActor = WaterBaseUtils.FindWaterBaseBuildingActor(map.Rules);
		var waterBaseDockActor = WaterBaseUtils.FindWaterBaseDockActor(map.Rules);

		var waterBases = map.ActorDefinitions.Where(a => a.Value.Value == waterBaseActor.Name).ToDictionary(n => n.Key);
		var waterBaseDocks = map.ActorDefinitions.Where(a => a.Value.Value == waterBaseDockActor.Name).ToList();

		// Docks need to be linked to Water Base, but also the link must be valid (i.e. such Water Base exists)
		foreach (var dock in waterBaseDocks)
		{
			var dockInit = dock.Value.NodeWithKeyOrDefault(WaterBaseDockInitName);
			if (dockInit == null)
				emitError($"Water Base Dock actor `{dock.Key}` is not linked with any Water Base building.");
			else if (!waterBases.ContainsKey(dockInit.Value.Value))
			{
				var incorrectActor =  map.ActorDefinitions.FirstOrDefault(n => n.Key == dockInit.Value.Value);
				if (incorrectActor != null)
					emitError($"Water Base Dock actor `{dock.Key}` is linked with an actor `{dockInit.Value.Value}`, which is not a Water Base but `{incorrectActor.Value.Value}`.");
				else
					emitError($"Water Base Dock actor `{dock.Key}` is linked with Water Base building `{dockInit.Value.Value}` that does not exist.");
			}
		}

		// Create 1:N lookup from Water Base to Docks to determine, if there are Water Bases with multiple Docks linked to just one Base
		var waterBaseToDockLookup = waterBaseDocks
			.Where(n => n.Value.Nodes.Any(n => n.Key == WaterBaseDockInitName))
			.ToLookup(p => p.Value.NodeWithKey(WaterBaseDockInitName).Value.Value);

		foreach (var item in waterBaseToDockLookup)
		{
			if (item.Count() > 1)
				emitError($"Multiple Water Base Docks are linked to Water Base `{item.Key}`: {string.Join(", ", item.Select(d => d.Key))}");
		}

		// All Water Bases need to have a Dock linked.
		foreach (var waterBase in waterBases.Values)
		{
			if (!waterBaseToDockLookup.Contains(waterBase.Key))
				emitError($"No Dock is linked to Water Base `{waterBase.Key}`.");
		}
	}
}
