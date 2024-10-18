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

namespace OpenRA.Mods.OpenE2140.Assets.FileFormats;

public class DatMapPlayer
{
	public readonly uint Id;
	public readonly DatMap.PlayerFlag Player;
	public readonly DatMap.PlayerFlag Enemies;
	public readonly DatMap.Faction Faction;
	public readonly uint Resources;

	public DatMapPlayer(Stream stream)
	{
		this.Id = stream.ReadUInt32();

		stream.Position += 256 * 4 + 24 * 2 * 4; // Cache

		this.Player = (DatMap.PlayerFlag)stream.ReadUInt32();
		this.Enemies = (DatMap.PlayerFlag)stream.ReadUInt32();
		stream.ReadUInt32(); // TODO unk
		this.Faction = (DatMap.Faction)stream.ReadUInt32();
		stream.ReadUInt32(); // Runtime-only player flag
		stream.ReadUInt32(); // Last command
		stream.ReadUInt32(); // Number of buildings
		stream.ReadUInt32(); // Last command Param 1
		stream.ReadUInt32(); // Last command Param 2
		stream.ReadUInt32(); // Last command X
		stream.ReadUInt32(); // Last command Y
		stream.ReadUInt32(); // TODO unk
		stream.ReadUInt32(); // Power
		this.Resources = stream.ReadUInt32();
		stream.ReadUInt32(); // TODO unk

		stream.Position += 20 * 4; // TODO Researched Technologies
		stream.Position += 256 * 4; // TODO Teams

		stream.ReadUInt32(); // Low Power
	}
}
