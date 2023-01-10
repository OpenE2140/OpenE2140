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

using OpenRA.FileSystem;
using OpenRA.Primitives;

namespace OpenRA.Mods.E2140.FileFormats;

public class Wd : IReadOnlyPackage
{
	private record WdEntry(uint Offset, uint Length);

	public string Name { get; }
	public IEnumerable<string> Contents => this.index.Keys;

	private readonly Dictionary<string, WdEntry> index = new Dictionary<string, WdEntry>();
	private readonly Stream stream;

	public Wd(Stream stream, string filename)
	{
		this.stream = stream;
		this.Name = filename;

		var numFiles = this.stream.ReadUInt32();

		if (numFiles == 0)
		{
			var lastOffset = 0u;

			for (var i = 0; i < 255; i++)
			{
				var offset = this.stream.ReadUInt32();

				if (offset > lastOffset)
					this.index.Add($"{i}.smp", new WdEntry(lastOffset + 0x400, offset - lastOffset));

				lastOffset = offset;
			}
		}
		else
		{
			for (var i = 0; i < numFiles; i++)
			{
				var entry = new WdEntry(stream.ReadUInt32(), stream.ReadUInt32());

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
	}

	public Stream? GetStream(string filename)
	{
		return this.index.TryGetValue(filename, out var entry) ? SegmentStream.CreateWithoutOwningStream(this.stream, entry.Offset, (int)entry.Length) : null;
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
