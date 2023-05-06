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

using System.Reflection;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Mods.OpenE2140.Assets.Extractors;
using OpenRA.Widgets;

namespace OpenRA.Mods.OpenE2140.Widgets.Logic;

public class AssetExtractLogic : ChromeLogic
{
	private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

	private readonly Widget widget;

	[ObjectCreator.UseCtor]
	public AssetExtractLogic(Widget widget)
	{
		this.widget = widget;

		var ticker = widget.GetOrNull<ButtonWidget>("EXTRACT_BUTTON");

		if (ticker != null)
			ticker.OnClick = this.Extract;
	}

	private void Extract()
	{
		var assetBrowser = this.widget.LogicObjects.OfType<AssetBrowserLogic>().FirstOrDefault();

		if (assetBrowser?.GetType().GetField("currentFilename", AssetExtractLogic.Flags)?.GetValue(assetBrowser) is not string currentFilename)
			return;

		if (assetBrowser.GetType().GetField("currentSprites", AssetExtractLogic.Flags)?.GetValue(assetBrowser) is Sprite[] sprites)
			SpriteExtractor.Extract(sprites, currentFilename);

		if (assetBrowser.GetType().GetField("currentSoundFormat", AssetExtractLogic.Flags)?.GetValue(assetBrowser) is ISoundFormat audio)
			AudioExtractor.Extract(audio, currentFilename);
	}
}
