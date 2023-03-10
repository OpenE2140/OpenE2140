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

namespace OpenRA.Mods.OpenE2140.Assets.VirtualAssets;

public class VirtualAssetsPackage : IReadOnlyPackage
{
	public const string Extension = ".VirtualAssets.yaml";

	private readonly Dictionary<string, Stream> contents;

	public string Name { get; }
	public IEnumerable<string> Contents => this.contents.Keys;

	public VirtualAssetsPackage(string name, IReadOnlyFileSystem fileSystem)
	{
		this.Name = name;

		this.contents = VirtualAssetsBuilder.BuildAssets(fileSystem, name);
	}

	public Stream? GetStream(string filename)
	{
		return this.contents.TryGetValue(filename, out var stream) ? SegmentStream.CreateWithoutOwningStream(stream, 0, (int)stream.Length) : null;
	}

	public bool Contains(string filename)
	{
		return this.contents.ContainsKey(filename);
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
		foreach (var stream in this.contents.Values)
			stream.Dispose();

		GC.SuppressFinalize(this);
	}
}
