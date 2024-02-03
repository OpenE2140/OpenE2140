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

using OpenRA.FileSystem;
using OpenRA.Mods.OpenE2140.Assets.FileFormats;

namespace OpenRA.Mods.OpenE2140.Assets.VirtualAssets;

public class VirtualAssetsPackage : IReadOnlyPackage
{
	private const string Extension = ".vspr";

	private readonly Dictionary<string, VirtualAssetsStream> contents = new Dictionary<string, VirtualAssetsStream>();

	public string Name { get; }
	public IEnumerable<string> Contents => this.contents.Keys;

	public VirtualAssetsPackage(Stream stream, string name, IReadOnlyFileSystem context)
	{
		this.Name = name;

		var yaml = MiniYaml.FromStream(stream, name);

		var sources = yaml.FirstOrDefault(e => e.Key == "Sources")?.Value.Nodes;
		var palettes = yaml.FirstOrDefault(e => e.Key == "Palettes")?.Value;

		if (sources == null)
			return;

		var paletteEffects = palettes == null ? new Dictionary<string, VirtualPalette>() : VirtualPalette.BuildPaletteEffects(palettes);

		foreach (var sourceNode in sources)
		{
			if (!context.TryOpen(sourceNode.Key, out var source))
				continue;

			var mix = new Mix(source);

			var suffix = sourceNode.Value.Value ?? string.Empty;
			var generate = yaml.FirstOrDefault(e => e.Key == "Generate")?.Value;

			if (generate == null)
				continue;

			foreach (var node in generate.Nodes)
				this.contents.Add(node.Key + suffix + VirtualAssetsPackage.Extension, new VirtualAssetsStream(mix, paletteEffects, node));
		}

		stream.Dispose();
	}

	public Stream? GetStream(string filename)
	{
		return this.contents.TryGetValue(filename, out var stream) ? stream : null;
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
