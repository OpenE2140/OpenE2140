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

public class DatMapObject
{
	public readonly ushort Id;
	public readonly DatMap.PlayerFlag Player;
	public readonly bool IsBuilding;
	public readonly ushort Unk;
	public readonly ushort X;
	public readonly ushort Y;
	public readonly ushort Type;

	public DatMapObject(Stream stream)
	{
		var temp = stream.ReadUInt16();
		this.Id = (ushort)(temp & (int)DatMap.PlayerFlag.MaskId);
		this.Player = (DatMap.PlayerFlag)(temp & (int)DatMap.PlayerFlag.MaskPlayer);
		this.IsBuilding = (temp & (int)DatMap.PlayerFlag.MaskIsBuilding) != 0;
		this.Unk = stream.ReadUInt16(); // TODO verify 2,5-12,15,30-45
		this.X = stream.ReadUInt16();
		this.Y = stream.ReadUInt16();
		this.Type = stream.ReadUInt16();
	}
}
