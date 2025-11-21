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

using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits;

[Desc("Modifies the terrain type underneath the actor's location.", "Make sure that the actor doesn't move, as the terrain is changed only on actor creation.")]
public class CustomChangesTerrainInfo : TraitInfo
{
	[FieldLoader.Require]
	[Desc("Type of terrain to change the cell under which the actor is created.")]
	public readonly string TerrainType = string.Empty;

	public override object Create(ActorInitializer init) { return new CustomChangesTerrain(this); }
}

public class CustomChangesTerrain : INotifyAddedToWorld, INotifyRemovedFromWorld
{
	private readonly CustomChangesTerrainInfo info;
	private byte? previousTerrain;

	public CustomChangesTerrain(CustomChangesTerrainInfo info)
	{
		this.info = info;
	}

	void INotifyAddedToWorld.AddedToWorld(Actor self)
	{
		var cell = self.Location;
		var map = self.World.Map;
		var terrain = map.Rules.TerrainInfo.GetTerrainIndex(this.info.TerrainType);
		this.previousTerrain = map.CustomTerrain[cell];
		map.CustomTerrain[cell] = terrain;
	}

	void INotifyRemovedFromWorld.RemovedFromWorld(Actor self)
	{
		if (this.previousTerrain == null)
			return;

		var cell = self.Location;
		var map = self.World.Map;
		map.CustomTerrain[cell] = this.previousTerrain.Value;
	}
}
