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

namespace OpenRA.Mods.OpenE2140.Traits.BotModules.BotModuleLogic;

public static class MapSearchHelper
{
	/// <summary>
	/// Finds all tiles within an annulus around <paramref name="center"/>.
	/// Uses spatial chunking to work around the engine's maximum tile search range limit.
	/// </summary>
	public static IEnumerable<CPos> FindTilesInAnnulusSpatialChunked(Map map, CPos center, int minRange, int maxRange, bool allowOutsideBounds = false)
	{
		var maxChunkSize = map.Grid.MaximumTileSearchRange;
		var results = new HashSet<CPos>();

		var minRangeSquared = minRange * minRange;
		var maxRangeSquared = maxRange * maxRange;

		var minBounds = center + new CVec(-maxRange, -maxRange);
		var maxBounds = center + new CVec(maxRange, maxRange);

		for (var chunkX = minBounds.X; chunkX <= maxBounds.X; chunkX += maxChunkSize)
		{
			for (var chunkY = minBounds.Y; chunkY <= maxBounds.Y; chunkY += maxChunkSize)
			{
				var chunkCenter = new CPos(chunkX, chunkY);

				foreach (var tile in map.FindTilesInCircle(chunkCenter, maxChunkSize, allowOutsideBounds))
				{
					var offset = tile - center;
					var distanceSquared = offset.LengthSquared;

					if (distanceSquared >= minRangeSquared && distanceSquared <= maxRangeSquared)
						results.Add(tile);
				}
			}
		}

		return results;
	}
}
