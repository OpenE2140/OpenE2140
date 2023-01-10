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

using JetBrains.Annotations;
using OpenRA.Mods.E2140.FileFormats;
using OpenRA.Video;

namespace OpenRA.Mods.E2140.VideoLoaders;

[UsedImplicitly]
public class FlcVideoLoader : IVideoLoader
{
	public bool TryParseVideo(Stream stream, bool useFramePadding, out IVideo? video)
	{
		var start = stream.Position;
		stream.Position = 4;
		var identifier = stream.ReadUInt16();
		stream.Position = start;

		video = null;

		if (identifier != 0xaf12)
			return false;

		video = new Flc(stream);

		return true;
	}
}
