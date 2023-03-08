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

using System.Reflection;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.OpenE2140.Helpers.Reflection;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.OpenE2140.Widgets;

/// <summary>
/// Modifies <see cref="ProductionTabsWidget"/> to make it possible to override <see cref="ProductionTabsWidget.Decorations"/>,
/// <see cref="ProductionTabsWidget.DecorationScrollLeft"/> and <see cref="ProductionTabsWidget.DecorationScrollRight"/> fields.
///
/// Temporary solution until PR #20635 is merged in OpenRA.
/// </summary>
public class ProductionTabsExWidget : ProductionTabsWidget, IFactionSpecificWidget
{
	private readonly ObjectFieldHelper<CachedTransform<(bool Disabled, bool Pressed, bool Hover, bool Focused, bool Highlighted), Sprite>> getLeftArrowImage;
	private readonly ObjectFieldHelper<CachedTransform<(bool Disabled, bool Pressed, bool Hover, bool Focused, bool Highlighted), Sprite>> getRightArrowImage;

	public Color Color = Color.White;
	public Color DoneColor = Color.Gold;

	public string Identifier = "";

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

	// TODO this should be a PR which allows to override the colors!
	// TODO protected Color TabColor = Color.White;
	// TODO protected Color TabColorDone = Color.Gold;
	// TODO protected Color TabColorDone = Color.Gold;
	public override void Draw()
	{
		base.Draw();

		var baseType = typeof(ProductionTabsWidget);
		var queueGroup = baseType.GetField("queueGroup", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(this) as string;
		var leftButtonRect = baseType.GetField("leftButtonRect", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(this) as Rectangle?;
		var rightButtonRect = baseType.GetField("rightButtonRect", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(this) as Rectangle?;
		var font = baseType.GetField("font", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(this) as SpriteFont;
		var listOffset = baseType.GetField("listOffset", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(this) as float?;

		if (queueGroup == null || leftButtonRect == null || rightButtonRect == null || font == null || listOffset == null)
			return;

		var tabs = this.Groups[queueGroup].Tabs.Where(t => t.Queue.BuildableItems().Any()).ToArray();

		if (!tabs.Any())
			return;

		var rb = this.RenderBounds;

		Game.Renderer.EnableScissor(new Rectangle(leftButtonRect.Value.Right, rb.Y + 1, rightButtonRect.Value.Left - leftButtonRect.Value.Right - 1, rb.Height));
		var origin = new int2(leftButtonRect.Value.Right - 1 + (int)listOffset, leftButtonRect.Value.Y);
		var contentWidth = 0;

		foreach (var tab in tabs)
		{
			var rect = new Rectangle(origin.X + contentWidth, origin.Y, this.TabWidth, rb.Height);
			contentWidth += this.TabWidth - 1;

			var textSize = font.Measure(tab.Name);
			var position = new int2(rect.X + (rect.Width - textSize.X) / 2, rect.Y + (rect.Height - textSize.Y) / 2);
			font.DrawTextWithContrast(tab.Name, position, tab.Queue.AllQueued().Any(i => i.Done) ? this.DoneColor : this.Color, Color.Black, 1);
		}

		Game.Renderer.DisableScissor();
	}

	string[] IFactionSpecificWidget.FieldsToOverride => new[] { nameof(this.Color), nameof(this.DoneColor) };

	string IFactionSpecificWidget.Identifier => this.Identifier;
}
