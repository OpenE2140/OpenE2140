using System.Text.RegularExpressions;

if (args.Length == 0)
	return;

var newName = args[0];
var newId = Regex.Replace(newName, "[^a-zA-Z0-9]", "");

if (newId == "")
	return;

var oldName = "Example";
var oldId = "Example";

void PatchFile(string path, Func<string, string> patch)
{
	File.WriteAllText(path, patch(File.ReadAllText(path)));
}

Directory.Move($"mods/{oldId}", $"mods/{newId}");

PatchFile($"mods/{newId}/mod.yaml", file => file
	.Replace($"Title: {oldName}", $"Title: {newName}")
	.Replace($"WindowTitle: {oldName}", $"WindowTitle: {newName}")
	.Replace($"${oldId}: {oldId}", $"${newId}: {newId}")
	.Replace($"OpenRA.Mods.{oldId}.dll", $"OpenRA.Mods.{newId}.dll")
	.Replace($"SupportsMapsFrom: {oldId}", $"SupportsMapsFrom: {newId}")
	.Replace($"{oldId}|", $"{newId}|")
);

PatchFile($"mods/{newId}/mod.chrome.yaml", file => file.Replace($"{oldId}|", $"{newId}|"));
PatchFile($"mods/{newId}/mod.content.yaml", file => file.Replace($"{oldId}|", $"{newId}|"));
PatchFile($"mods/{newId}/maps/example/map.yaml",
	file => file.Replace($"RequiresMod: {oldId}", $"RequiresMod: {newId}"));
PatchFile($"mods/{newId}/maps/shellmap/map.yaml",
	file => file.Replace($"RequiresMod: {oldId}", $"RequiresMod: {newId}"));

Directory.Move($"OpenRA.Mods.{oldId}", $"OpenRA.Mods.{newId}");

File.Move($"OpenRA.Mods.{newId}/OpenRA.Mods.{oldId}.csproj", $"OpenRA.Mods.{newId}/OpenRA.Mods.{newId}.csproj");
File.Move($"{oldId}.sln", $"{newId}.sln");

PatchFile($"{newId}.sln", file => file.Replace($"OpenRA.Mods.{oldId}", $"OpenRA.Mods.{newId}"));

PatchFile("mod.config", file => file
	.Replace($"MOD_ID=\"{oldId}\"", $"MOD_ID=\"{newId}\"")
	.Replace($"PACKAGING_DISPLAY_NAME=\"{oldName}\"", $"PACKAGING_DISPLAY_NAME=\"{newName}\"")
	.Replace($"PACKAGING_INSTALLER_NAME=\"{oldId}\"", $"PACKAGING_INSTALLER_NAME=\"{newId}\"")
	.Replace($"PACKAGING_AUTHORS=\"{oldName} authors\"", $"PACKAGING_AUTHORS=\"{newName} authors\"")
	.Replace($"PACKAGING_WINDOWS_LAUNCHER_NAME=\"{oldName}\"", $"PACKAGING_WINDOWS_LAUNCHER_NAME=\"{newName}\"")
	.Replace($"PACKAGING_WINDOWS_INSTALL_DIR_NAME=\"{oldName}\"", $"PACKAGING_WINDOWS_INSTALL_DIR_NAME=\"{newName}\"")
	.Replace($"PACKAGING_WINDOWS_REGISTRY_KEY=\"{oldId}\"", $"PACKAGING_WINDOWS_REGISTRY_KEY=\"{newId}\"")
);
