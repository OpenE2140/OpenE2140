using Microsoft.Win32;
using System.Runtime.InteropServices;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Installer;

namespace OpenRA.Mods.OpenE2140.Installer
{
	public class WindowsGogSourceResolver : ISourceResolver
	{
		public string? FindSourcePath(ModContent.ModSource modSource)
		{
			var appId = modSource.Type.NodeWithKeyOrDefault("AppId");

			if (appId == null)
				return null;

			if (Platform.CurrentPlatform != PlatformType.Windows)
				return null;

			// We need an extra check for the platform here to silence a warning when the registry is accessed
			// TODO: Remove this once our platform checks use the same method
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				return null;

			var prefixes = new[] { "HKEY_LOCAL_MACHINE\\Software\\", "HKEY_LOCAL_MACHINE\\SOFTWARE\\Wow6432Node\\" };

			foreach (var prefix in prefixes)
			{
				if (Registry.GetValue($"{prefix}GOG.com\\Games\\{appId.Value.Value}", "path", null) is not string installDir)
					continue;

				if (InstallerUtils.IsValidSourcePath(installDir, modSource))
					return installDir;
			}

			return null;
		}

		public Availability GetAvailability()
		{
			return Platform.CurrentPlatform == PlatformType.Windows ? Availability.DigitalInstall : Availability.Unavailable;
		}
	}
}
