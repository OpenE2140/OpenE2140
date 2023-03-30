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

using ICSharpCode.SharpZipLib.Zip;
using JetBrains.Annotations;
using OpenRA.Mods.Common.Installer;
using OpenRA.Mods.Common.Widgets.Logic;
using FS = OpenRA.FileSystem.FileSystem;

namespace OpenRA.Mods.OpenE2140.Installer;

// TODO remove this class when https://github.com/OpenRA/OpenRA/pull/20776 is merged and engine is rebased!
[UsedImplicitly]
public class ExtractZipSourceAction : ISourceAction
{
	public void RunActionOnSource(MiniYaml actionYaml, string path, ModData modData, List<string> extracted, Action<string> updateMessage)
	{
		var zipPath = actionYaml.Value.StartsWith("^")
			? Platform.ResolvePath(actionYaml.Value)
			: FS.ResolveCaseInsensitivePath(Path.Combine(path, actionYaml.Value));

		using var zipFile = new ZipFile(File.OpenRead(zipPath));

		foreach (var node in actionYaml.Nodes)
		{
			var targetPath = Platform.ResolvePath(node.Key);
			var sourcePath = node.Value.Value;

			if (File.Exists(targetPath))
			{
				Log.Write("install", "Skipping installed file " + targetPath);

				continue;
			}

			Log.Write("install", $"Extracting {sourcePath} -> {targetPath}");

			var displayFilename = Path.GetFileName(targetPath);

			Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? "");

			var sourceStream = zipFile.GetInputStream(zipFile.GetEntry(sourcePath));
			using var targetStream = File.OpenWrite(targetPath);

			sourceStream.CopyTo(targetStream);

			updateMessage(
				modData.Translation.GetString(InstallFromSourceLogic.ExtracingProgress, Translation.Arguments("filename", displayFilename, "progress", 100))
			);

			extracted.Add(targetPath);
		}
	}
}
