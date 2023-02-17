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
public class ProductionTabsExWidget : ProductionTabsWidget
{
	public string ArrowButton = "button";
	public string TabButton = "button";

	private readonly ObjectFieldHelper<CachedTransform<(bool Disabled, bool Pressed, bool Hover, bool Focused, bool Highlighted), Sprite>> getLeftArrowImage;
	private readonly ObjectFieldHelper<CachedTransform<(bool Disabled, bool Pressed, bool Hover, bool Focused, bool Highlighted), Sprite>> getRightArrowImage;

	private readonly ObjectFieldHelper<int> contentWidth;
	private readonly ObjectFieldHelper<float> listOffset;
	private readonly ObjectFieldHelper<bool> leftPressed;
	private readonly ObjectFieldHelper<bool> rightPressed;
	private readonly ObjectFieldHelper<SpriteFont> font;
	private readonly ObjectFieldHelper<Rectangle> leftButtonRect;
	private readonly ObjectFieldHelper<Rectangle> rightButtonRect;

	[ObjectCreator.UseCtorAttribute]
	public ProductionTabsExWidget(World world)
		: base(world)
	{
		var reflectionHelper = new ReflectionHelper<ProductionTabsWidget>(this);

		this.getLeftArrowImage = reflectionHelper.GetField(this.getLeftArrowImage, "getLeftArrowImage");
		this.getRightArrowImage = reflectionHelper.GetField(this.getRightArrowImage, "getRightArrowImage");

		this.contentWidth = reflectionHelper.GetField(this.contentWidth, "contentWidth");
		this.font = reflectionHelper.GetField(this.font, "font");
		this.listOffset = reflectionHelper.GetField(this.listOffset, "listOffset");
		this.leftPressed = reflectionHelper.GetField(this.leftPressed, "leftPressed");
		this.rightPressed = reflectionHelper.GetField(this.rightPressed, "rightPressed");
		this.leftButtonRect = reflectionHelper.GetField(this.leftButtonRect, "leftButtonRect");
		this.rightButtonRect = reflectionHelper.GetField(this.rightButtonRect, "rightButtonRect");
	}

	public override void Initialize(WidgetArgs args)
	{
		base.Initialize(args);

		this.getLeftArrowImage.Value = WidgetUtils.GetCachedStatefulImage(this.Decorations, this.DecorationScrollLeft);
		this.getRightArrowImage.Value = WidgetUtils.GetCachedStatefulImage(this.Decorations, this.DecorationScrollRight);
	}

	public override void Draw()
	{
		var tabs = this.Groups[this.QueueGroup].Tabs.Where(t => t.Queue.BuildableItems().Any());

		if (!tabs.Any())
			return;

		var rb = this.RenderBounds;

		var leftDisabled = this.listOffset.Value >= 0;
		var leftHover = Ui.MouseOverWidget == this && this.leftButtonRect.Value.Contains(Viewport.LastMousePos);

		var rightDisabled = this.listOffset.Value
			<= this.Bounds.Width - this.rightButtonRect.Value.Width - this.leftButtonRect.Value.Width - this.contentWidth.Value;

		var rightHover = Ui.MouseOverWidget == this && this.rightButtonRect.Value.Contains(Viewport.LastMousePos);

		WidgetUtils.DrawPanel(this.Background, rb);
		ButtonWidget.DrawBackground(this.ArrowButton, this.leftButtonRect.Value, leftDisabled, this.leftPressed.Value, leftHover, false);
		ButtonWidget.DrawBackground(this.ArrowButton, this.rightButtonRect.Value, rightDisabled, this.rightPressed.Value, rightHover, false);

		var leftArrowImage = this.getLeftArrowImage.Value!.Update((leftDisabled, this.leftPressed.Value, leftHover, false, false));

		WidgetUtils.DrawSprite(
			leftArrowImage,
			new float2(
				this.leftButtonRect.Value.Left + (int)((this.leftButtonRect.Value.Width - leftArrowImage.Size.X) / 2),
				this.leftButtonRect.Value.Top + (int)((this.leftButtonRect.Value.Height - leftArrowImage.Size.Y) / 2)
			)
		);

		var rightArrowImage = this.getRightArrowImage.Value!.Update((rightDisabled, this.rightPressed.Value, rightHover, false, false));

		WidgetUtils.DrawSprite(
			rightArrowImage,
			new float2(
				this.rightButtonRect.Value.Left + (int)((this.rightButtonRect.Value.Width - rightArrowImage.Size.X) / 2),
				this.rightButtonRect.Value.Top + (int)((this.rightButtonRect.Value.Height - rightArrowImage.Size.Y) / 2)
			)
		);

		// Draw tab buttons
		Game.Renderer.EnableScissor(
			new Rectangle(this.leftButtonRect.Value.Right, rb.Y + 1, this.rightButtonRect.Value.Left - this.leftButtonRect.Value.Right - 1, rb.Height)
		);

		var origin = new int2(this.leftButtonRect.Value.Right - 1 + (int)this.listOffset.Value, this.leftButtonRect.Value.Y);
		this.contentWidth.Value = 0;

		foreach (var tab in tabs)
		{
			var rect = new Rectangle(origin.X + this.contentWidth.Value, origin.Y, this.TabWidth, rb.Height);
			var hover = !leftHover && !rightHover && Ui.MouseOverWidget == this && rect.Contains(Viewport.LastMousePos);
			var highlighted = tab.Queue == this.CurrentQueue;
			ButtonWidget.DrawBackground(this.TabButton, rect, false, false, hover, highlighted);
			this.contentWidth.Value += this.TabWidth - 1;

			var textSize = this.font.Value!.Measure(tab.Name);
			var position = new int2(rect.X + (rect.Width - textSize.X) / 2, rect.Y + (rect.Height - textSize.Y) / 2);
			this.font.Value.DrawTextWithContrast(tab.Name, position, tab.Queue.AllQueued().Any(i => i.Done) ? Color.Gold : Color.White, Color.Black, 1);
		}

		Game.Renderer.DisableScissor();
	}
}
