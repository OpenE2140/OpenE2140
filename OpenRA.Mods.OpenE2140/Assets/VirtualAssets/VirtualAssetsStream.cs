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

using OpenRA.Mods.OpenE2140.Assets.FileFormats;

namespace OpenRA.Mods.OpenE2140.Assets.VirtualAssets;

public class VirtualAssetsStream : Stream
{
	// TODO If we manage to unhardcode Mix here, VirtualAssets could apply to any source!
	public readonly Mix Source;
	public readonly Dictionary<string, VirtualPalette> PaletteEffects;
	public readonly MiniYamlNode Node;

	public override bool CanRead => true;
	public override bool CanSeek => true;
	public override bool CanWrite => true;
	public override long Length => 0;
	public override long Position { get => 0; set { } }

	public VirtualAssetsStream(Mix source, Dictionary<string, VirtualPalette> paletteEffects, MiniYamlNode node)
	{
		this.Source = source;
		this.PaletteEffects = paletteEffects;
		this.Node = node;
	}

	public override void Flush()
	{
	}

	public override int Read(byte[] buffer, int offset, int count)
	{
		return count;
	}

	public override long Seek(long offset, SeekOrigin origin)
	{
		return 0;
	}

	public override void SetLength(long value)
	{
	}

	public override void Write(byte[] buffer, int offset, int count)
	{
	}
}
