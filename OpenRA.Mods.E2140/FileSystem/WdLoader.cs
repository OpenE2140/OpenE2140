using System;
using System.IO;
using OpenRA.FileSystem;
using OpenRA.Mods.E2140.FileFormats;

namespace OpenRA.Mods.E2140.FileSystem
{
	public class WdLoader : IPackageLoader
	{
		public bool TryParsePackage(Stream s, string filename, OpenRA.FileSystem.FileSystem context, out IReadOnlyPackage package)
		{
			if (!filename.EndsWith(".wd", StringComparison.OrdinalIgnoreCase))
			{
				package = null;
				return false;
			}

			package = new Wd(s, filename);
			return true;
		}
	}
}
