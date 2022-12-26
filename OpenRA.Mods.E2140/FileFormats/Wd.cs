#region Copyright & License Information

/*
 * Copyright 2007-2022 The Earth 2140 Developers (see AUTHORS)
 * This file is part of Earth 2140, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */

#endregion

using OpenRA.FileSystem;
using OpenRA.Primitives;

namespace OpenRA.Mods.E2140.FileFormats;

public class Wd : IReadOnlyPackage
{
	private class WdEntry
	{
		public int Offset;
		public int Length;
	}

	public string Name { get; }
	public IEnumerable<string> Contents => this.index.Keys;

	private readonly Dictionary<string, WdEntry> index = new Dictionary<string, WdEntry>();
	private readonly Stream stream;

	public Wd(Stream stream, string filename)
	{
		this.stream = stream;
		this.Name = filename;

		var numFiles = stream.ReadUInt32();

		if (numFiles == 0)
			return; // TODO implement sound container support

		for (var i = 0; i < numFiles; i++)
		{
			var entry = new WdEntry { Offset = stream.ReadInt32(), Length = stream.ReadInt32() };

			var unk1 = stream.ReadUInt32(); // 0x00
			var unk2 = stream.ReadUInt32(); // 0x00
			var unk3 = stream.ReadUInt32(); // TODO has a value in MIX.WD

			var filePathOffset = stream.ReadUInt32();

			var originalPosition = stream.Position;
			stream.Position = numFiles * 24 + 8 + filePathOffset;
			this.index.Add(stream.ReadASCIIZ(), entry);
			stream.Position = originalPosition;
		}
	}

	public Stream? GetStream(string filename)
	{
		return !this.index.TryGetValue(filename, out var entry) ? null : SegmentStream.CreateWithoutOwningStream(this.stream, entry.Offset, entry.Length);
	}

	public bool Contains(string filename)
	{
		return this.index.ContainsKey(filename);
	}

	public IReadOnlyPackage? OpenPackage(string filename, OpenRA.FileSystem.FileSystem context)
	{
		var childStream = this.GetStream(filename);

		if (childStream == null)
			return null;

		if (context.TryParsePackage(childStream, filename, out var package))
			return package;

		childStream.Dispose();

		return null;
	}

	public void Dispose()
	{
		GC.SuppressFinalize(this);

		this.stream.Dispose();
	}
}
