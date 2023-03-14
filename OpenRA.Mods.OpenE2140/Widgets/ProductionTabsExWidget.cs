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
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.OpenE2140.Helpers.Reflection;
using OpenRA.Widgets;

namespace OpenRA.Mods.OpenE2140.Widgets;


// TODO Temporary solution until PR #20635 is merged in OpenRA.
[UsedImplicitly]
public class ProductionTabsExWidget : ProductionTabsWidget, IFactionSpecificWidget
{
	private readonly ObjectFieldHelper<CachedTransform<(bool Disabled, bool Pressed, bool Hover, bool Focused, bool Highlighted), Sprite>> getLeftArrowImage;
	private readonly ObjectFieldHelper<CachedTransform<(bool Disabled, bool Pressed, bool Hover, bool Focused, bool Highlighted), Sprite>> getRightArrowImage;

	public readonly string Identifier = "";

	[ObjectCreator.UseCtorAttribute]
	public ProductionTabsExWidget(World world)
		: base(world)
	{
		var reflectionHelper = new ReflectionHelper<ProductionTabsWidget>(this);

		this.getLeftArrowImage = reflectionHelper.GetField(this.getLeftArrowImage, "getLeftArrowImage");
		this.getRightArrowImage = reflectionHelper.GetField(this.getRightArrowImage, "getRightArrowImage");
	}

	public override void Initialize(WidgetArgs args)
	{
		base.Initialize(args);

		this.RefreshCaches();
	}

	public void RefreshCaches()
	{
		this.getLeftArrowImage.Value = WidgetUtils.GetCachedStatefulImage(this.Decorations, this.DecorationScrollLeft);
		this.getRightArrowImage.Value = WidgetUtils.GetCachedStatefulImage(this.Decorations, this.DecorationScrollRight);
	}

	string[] IFactionSpecificWidget.FieldsToOverride => new[] { nameof(this.TabColor), nameof(this.TabColorDone) };

	string IFactionSpecificWidget.Identifier => this.Identifier;
}
