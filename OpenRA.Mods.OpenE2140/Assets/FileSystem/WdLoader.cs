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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using OpenRA.FileSystem;
using OpenRA.Mods.OpenE2140.Assets.FileFormats;

namespace OpenRA.Mods.OpenE2140.Assets.FileSystem;

[UsedImplicitly]
public class WdLoader : IPackageLoader
{
	public bool TryParsePackage(Stream stream, string filename, OpenRA.FileSystem.FileSystem context, [NotNullWhen(true)] out IReadOnlyPackage? package)
	{
		if (!filename.EndsWith(".wd", StringComparison.OrdinalIgnoreCase))
		{
			package = null;

			return false;
		}

		package = new Wd(stream, filename);

		return true;
	}
}
