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

using JetBrains.Annotations;
using OpenRA.Mods.Common.Widgets;

namespace OpenRA.Mods.OpenE2140.Widgets;

[UsedImplicitly]
public class LoopedVideoPlayerWidget : VideoPlayerWidget
{
	private string? video;

	public override void Draw()
	{
		if (this.video == null)
			return;

		base.Draw();

		if (!this.Paused)
			return;

		this.Play();
		base.Draw();
	}

	public void SetVideo(string? video)
	{
		this.video = video;
		this.Stop();

		if (video != null)
			this.LoadAndPlay(video);
	}
}
