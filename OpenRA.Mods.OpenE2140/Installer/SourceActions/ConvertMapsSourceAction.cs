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

using System.Text.RegularExpressions;
using OpenRA.Mods.Common.Installer;
using OpenRA.Mods.OpenE2140.Assets.FileFormats;
using FS = OpenRA.FileSystem.FileSystem;

namespace OpenRA.Mods.OpenE2140.Installer.SourceActions;

public class ConvertMapsSourceAction : ISourceAction
{
	public void RunActionOnSource(MiniYaml actionYaml, string path, ModData modData, List<string> extracted, Action<string> updateMessage)
	{
		var wdPath = actionYaml.Value.StartsWith('^')
			? Platform.ResolvePath(actionYaml.Value)
			: FS.ResolveCaseInsensitivePath(Path.Combine(path, actionYaml.Value));

		using var wd = new Wd(File.OpenRead(wdPath), Path.GetFileName(wdPath));

		var log = "";

		foreach (var node in actionYaml.Nodes)
		{
			var map = new DatMap(
				wd.GetStream($"{node.Value.Value}.DAT") ?? throw new Exception(),
				Path.GetFileName(node.Key),
				Path.GetFileName(node.Value.Value)
			);

			var name = Path.GetFileName(node.Key);

			if (!name.StartsWith("ed_") && !name.StartsWith("ucs_"))
				name = Regex.Replace(map.Name.Trim(), @"^\(.+?\)\s*", "");

			// Top left corner -> calculate center tile.
			var cameraX = (map.CameraX + 600 / 2) / 64;
			var cameraY = (map.CameraY + 800 / 2) / 64;

			var ini = wd.GetStream($"{node.Value.Value}.INI") is Stream iniStream ? new Ini(iniStream) : null;
		}

		File.AppendAllText($"out/players.txt", log);
	}
}
