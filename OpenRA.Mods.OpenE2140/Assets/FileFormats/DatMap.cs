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

using System.Text;

namespace OpenRA.Mods.OpenE2140.Assets.FileFormats;

public class DatMap
{
	[Flags]
	public enum TileFlag : ushort
	{
		Land = 0x01,
		Water = 0x02,
		Shore = 0x08,
		Blocked = 0x10,
		Ore = 0x20,
		Unbuildable = 0x40
	}

	[Flags]
	public enum PlayerFlag : ushort
	{
		None = 0x0000,
		Player1 = 0x0200,
		Player2 = 0x0400,
		Player3 = 0x0800,
		Player4 = 0x1000,
		Player5 = 0x2000,
		Player6 = 0x4000,
		MaskId = 0x01ff,
		MaskPlayer = 0x7e00,
		MaskIsBuilding = 0x1000
	}

	public enum Faction
	{
		Ed = 0x00,
		Ucs = 0x01
	}

	private const byte MaxSize = 128;

	public readonly string Name;

	public readonly uint Width;
	public readonly uint Height;

	public readonly uint TechLevel;
	public readonly uint TileSet;

	public readonly uint CameraX;
	public readonly uint CameraY;

	public readonly byte[] Tiles; // TODO remove out of bounds tiles!
	public readonly TileFlag[] TileFlags; // TODO remove out of bounds tiles!

	public readonly DatMapUnit[] Units;
	public readonly DatMapBuilding[] Buildings;
	public readonly DatMapObject[] Objects;
	public readonly DatMapTrigger[] Triggers;

	public DatMap(Stream stream, string targetName, string file)
	{
		// 0xf8 is sometimes used instead of 0x20 (space)
		this.Name = Encoding.ASCII.GetString(stream.ReadBytes(31).Select(e => e == 0xf8 ? (byte)0x20 : e).ToArray()).Split('\0')[0];

		var tileFlags = new TileFlag[DatMap.MaxSize * DatMap.MaxSize];

		for (var i = 0; i < tileFlags.Length; i++)
			tileFlags[i] = (TileFlag)stream.ReadUInt16();

		var tiles = stream.ReadBytes(DatMap.MaxSize * DatMap.MaxSize);

		var units = new DatMapUnit[512];

		for (var i = 0; i < units.Length; i++)
			units[i] = new DatMapUnit(stream);

		var buildings = new DatMapBuilding[256];

		for (var i = 0; i < buildings.Length; i++)
			buildings[i] = new DatMapBuilding(stream);

		var objects = new DatMapObject[512];

		for (var i = 0; i < objects.Length; i++)
			objects[i] = new DatMapObject(stream);

		this.CameraX = stream.ReadUInt32();
		this.CameraY = stream.ReadUInt32();
		this.Width = stream.ReadUInt32();
		this.Height = stream.ReadUInt32();
		var unk1 = stream.ReadUInt32();
		var unk2 = stream.ReadUInt32();
		var unk3 = stream.ReadUInt32();
		var unk4 = stream.ReadUInt32(); // TODO 2 maps have 56, all others use 0. Broken when 56?
		var unk5 = stream.ReadUInt32();
		var unk6 = stream.ReadUInt32();
		this.TechLevel = stream.ReadUInt32();
		var unk8 = stream.ReadUInt32();
		var unk9 = stream.ReadUInt32();
		this.TileSet = stream.ReadUInt32();

		if (unk1 != 10 || unk2 != 9 || unk3 != 0 || (unk4 != 0 && unk4 != 56) || unk5 != 0 || unk6 != 0 || unk8 != 0 || unk9 != 0)
			throw new Exception();

		var players = new DatMapPlayer[6]; // TODO

		for (var i = 0; i < players.Length; i++)
			players[i] = new DatMapPlayer(stream);

		var triggers = new DatMapTrigger?[20]; // TODO

		for (var i = 0; i < triggers.Length; i++)
			triggers[i] = DatMapTrigger.Read(stream);

		// This is likely all AI after here...

		var unk10 = stream.ReadUInt32();
		var unk11 = stream.ReadUInt32();
		var unk12 = stream.ReadUInt32();
		var unk13 = stream.ReadUInt32();
		var unk14 = stream.ReadUInt32();
		var unk15 = stream.ReadUInt32();
		var unk16 = stream.ReadUInt32();
		var unk17 = stream.ReadUInt32();
		var unk18 = stream.ReadUInt32();
		var unk19 = stream.ReadUInt32();
		var unk20 = stream.ReadUInt32(); // TODO
		var width2 = stream.ReadUInt32();
		var height2 = stream.ReadUInt32();
		var unk23 = stream.ReadUInt32(); // TODO
		var unk24 = stream.ReadUInt32(); // TODO

		if (unk10 != 0 || unk11 != 0 || unk12 != 0 || unk13 != 0 || unk14 != 0 || unk15 != 0 || unk16 != 0 || unk17 != 0 || unk18 != 0 || unk19 != 0)
			throw new Exception();

		if (width2 != this.Width || height2 != this.Height)
			throw new Exception();

		var unk25 = stream.ReadBytes(DatMap.MaxSize * DatMap.MaxSize); // TODO highest bit is a different bool. Points to unk11 entry???

		var unk26 = new uint[256]; // TODO

		for (var i = 0; i < unk26.Length; i++)
			unk26[i] = stream.ReadUInt32();

		var unk27 = new byte[1024]; // TODO 0,1,2

		for (var i = 0; i < unk27.Length; i++)
			unk27[i] = stream.ReadUInt8();

		var className = Encoding.ASCII.GetString(stream.ReadBytes(15)).Split('\0')[0];

		if (className != "class CTerrain")
			throw new Exception();

		// Populate the arrays without the unused data.
		this.Tiles = new byte[this.Width * this.Height];
		this.TileFlags = new TileFlag[this.Width * this.Height];

		for (var y = 0; y < this.Width; y++)
		{
			Array.Copy(tiles, y * DatMap.MaxSize, this.Tiles, y * this.Height, this.Height);
			Array.Copy(tileFlags, y * DatMap.MaxSize, this.TileFlags, y * this.Height, this.Height);
		}

		this.Units = units.Where(unit => unit.Id != 0 && unit.Valid).ToArray();
		this.Buildings = buildings.Where(building => building.Id != 0 && building.Valid).ToArray();
		this.Objects = objects.Where(obj => obj.Id != 0 && obj.X < this.Width && obj.Y < this.Height).ToArray();
		this.Triggers = triggers.OfType<DatMapTrigger>().ToArray();

		// DEBUG
		//Console.WriteLine($"{file,-16}\t{targetName,-31}");

		/*var pixels = new byte[this.Width * this.Height * 4];

		for (var y = 0; y < this.Height; y++)
		for (var x = 0; x < this.Width; x++)
		{
			pixels[(y * this.Width + x) * 4 + 0] = (byte)((byte)tileFlags[x * DatMap.MaxSize + y] * 2);
			pixels[(y * this.Width + x) * 4 + 1] = unk10[x * DatMap.MaxSize + y];
			pixels[(y * this.Width + x) * 4 + 3] = 0xff;
		}

		Array.Fill(pixels, (byte)0xff, (int)((this.CameraY * this.Width + this.CameraX) * 4), 4);

		foreach (var unit in this.Units)
			pixels[(unit.Y * this.Width + unit.X) * 4 + 2] = (byte)unit.Type;

		foreach (var building in this.Buildings)
			pixels[(building.Y * this.Width + building.X) * 4 + 2] = (byte)building.Type;

		foreach (var obj in this.Objects)
			pixels[(obj.Y * this.Width + obj.X) * 4 + 2] = (byte)obj.Type;

		new Png(pixels, SpriteFrameType.Rgba32, (int)this.Width, (int)this.Height).Save($"out/debug/{targetName}.png");*/
	}
}
