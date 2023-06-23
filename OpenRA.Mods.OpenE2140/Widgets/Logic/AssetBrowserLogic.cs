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
using JetBrains.Annotations;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.OpenE2140.Assets.Extractors;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Widgets;

namespace OpenRA.Mods.OpenE2140.Widgets.Logic;

[UsedImplicitly]
public class AssetBrowserLogic : Common.Widgets.Logic.AssetBrowserLogic
{
	private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

	[ObjectCreator.UseCtor]
	public AssetBrowserLogic(Widget widget, Action onExit, ModData modData, WorldRenderer worldRenderer)
		: base(widget, onExit, modData, worldRenderer)
	{
		var closeButton = widget.GetOrNull<ButtonWidget>("CLOSE_BUTTON");

		var extractButton = (ButtonWidget)closeButton.Clone();
		extractButton.Id = "EXTRACT_BUTTON";
		extractButton.X = extractButton.X.Concat($" - {closeButton.Width} - 20");
		extractButton.Text = "Extract";
		extractButton.GetText = () => extractButton.Text;
		extractButton.OnClick = this.Extract;

		closeButton.Parent.AddChild(extractButton);

		extractButton.Initialize(new WidgetArgs());
	}

	private void Extract()
	{
		if (this.GetType().BaseType?.GetField("currentFilename", AssetBrowserLogic.Flags)?.GetValue(this) is not string currentFilename)
			return;

		if (this.GetType().BaseType?.GetField("currentSprites", AssetBrowserLogic.Flags)?.GetValue(this) is Sprite[] sprites)
			SpriteExtractor.Extract(sprites, currentFilename);

		if (this.GetType().BaseType?.GetField("currentSoundFormat", AssetBrowserLogic.Flags)?.GetValue(this) is ISoundFormat audio)
			AudioExtractor.Extract(audio, currentFilename);
	}
}
