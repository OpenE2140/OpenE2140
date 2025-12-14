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

		var extractButton = closeButton.Clone();
		extractButton.Id = "EXTRACT_BUTTON";
		extractButton.X = extractButton.X.Concat($" - {closeButton.Width} - 20");
		extractButton.Text = "Extract";
		extractButton.GetText = () => extractButton.Text;
		extractButton.OnClick = this.Extract;

		closeButton.Parent.AddChild(extractButton);

		extractButton.Initialize([]);
	}

	private void Extract()
	{
		if (!this.TryGetFieldValue<string>("currentFilename", out var currentFilename))
			return;

		if (this.TryGetFieldValue<Sprite[]>("currentSprites", out var sprites))
			SpriteExtractor.Extract(sprites, currentFilename);

		if (this.TryGetFieldValue<ISoundFormat>("currentSoundFormat", out var audio))
			AudioExtractor.Extract(audio, currentFilename);

		if (this.TryGetFieldValue<bool?>("isVideoLoaded", out var isVideoLoaded) && isVideoLoaded == true
			&& this.TryGetFieldValue<VideoPlayerWidget>("player", out var videoPlayerWidget)
			&& videoPlayerWidget.Video != null)
			VideoExtractor.ExtractVideo(videoPlayerWidget.Video, currentFilename);
	}

	private bool TryGetFieldValue<T>(string fieldName, [NotNullWhen(true)] out T? value)
	{
		value = default;

		var baseType = this.GetType().BaseType;
		if (baseType == null)
			return false;

		if (baseType.GetField(fieldName, AssetBrowserLogic.Flags)?.GetValue(this) is T fieldValue)
		{
			value = fieldValue;
			return true;
		}

		return false;
	}
}
