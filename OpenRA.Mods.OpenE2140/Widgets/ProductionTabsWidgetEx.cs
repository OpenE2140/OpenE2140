using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.OpenE2140.Helpers;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.OpenE2140.Widgets
{
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

		private readonly FieldHelper<CachedTransform<(bool Disabled, bool Pressed, bool Hover, bool Focused, bool Highlighted), Sprite>> getLeftArrowImage;
		private readonly FieldHelper<CachedTransform<(bool Disabled, bool Pressed, bool Hover, bool Focused, bool Highlighted), Sprite>> getRightArrowImage;

		private readonly FieldHelper<int> contentWidth;
		private readonly FieldHelper<float> listOffset;
		private readonly FieldHelper<bool> leftPressed;
		private readonly FieldHelper<bool> rightPressed;
		private readonly FieldHelper<SpriteFont> font;
		private readonly FieldHelper<Rectangle> leftButtonRect;
		private readonly FieldHelper<Rectangle> rightButtonRect;


		[ObjectCreator.UseCtor]
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

			getLeftArrowImage.Value = WidgetUtils.GetCachedStatefulImage(Decorations, DecorationScrollLeft);
			getRightArrowImage.Value = WidgetUtils.GetCachedStatefulImage(Decorations, DecorationScrollRight);
		}

		public override void Draw()
		{
			var tabs = Groups[QueueGroup].Tabs.Where(t => t.Queue.BuildableItems().Any());

			if (!tabs.Any())
				return;

			var rb = RenderBounds;

			var leftDisabled = listOffset.Value >= 0;
			var leftHover = Ui.MouseOverWidget == this && leftButtonRect.Value.Contains(Viewport.LastMousePos);
			var rightDisabled = listOffset.Value <= Bounds.Width - rightButtonRect.Value.Width - leftButtonRect.Value.Width - contentWidth.Value;
			var rightHover = Ui.MouseOverWidget == this && rightButtonRect.Value.Contains(Viewport.LastMousePos);

			WidgetUtils.DrawPanel(Background, rb);
			ButtonWidget.DrawBackground(ArrowButton, leftButtonRect.Value, leftDisabled, leftPressed.Value, leftHover, false);
			ButtonWidget.DrawBackground(ArrowButton, rightButtonRect.Value, rightDisabled, rightPressed.Value, rightHover, false);

			var leftArrowImage = getLeftArrowImage.Value!.Update((leftDisabled, this.leftPressed.Value, leftHover, false, false));
			WidgetUtils.DrawSprite(leftArrowImage,
				new float2(leftButtonRect.Value.Left + (int)((leftButtonRect.Value.Width - leftArrowImage.Size.X) / 2), leftButtonRect.Value.Top + (int)((leftButtonRect.Value.Height - leftArrowImage.Size.Y) / 2)));

			var rightArrowImage = getRightArrowImage.Value!.Update((rightDisabled, rightPressed.Value, rightHover, false, false));
			WidgetUtils.DrawSprite(rightArrowImage,
				new float2(rightButtonRect.Value.Left + (int)((rightButtonRect.Value.Width - rightArrowImage.Size.X) / 2), rightButtonRect.Value.Top + (int)((rightButtonRect.Value.Height - rightArrowImage.Size.Y) / 2)));

			// Draw tab buttons
			Game.Renderer.EnableScissor(new Rectangle(leftButtonRect.Value.Right, rb.Y + 1, rightButtonRect.Value.Left - leftButtonRect.Value.Right - 1, rb.Height));
			var origin = new int2(leftButtonRect.Value.Right - 1 + (int)listOffset.Value, leftButtonRect.Value.Y);
			contentWidth.Value = 0;

			foreach (var tab in tabs)
			{
				var rect = new Rectangle(origin.X + contentWidth.Value, origin.Y, TabWidth, rb.Height);
				var hover = !leftHover && !rightHover && Ui.MouseOverWidget == this && rect.Contains(Viewport.LastMousePos);
				var highlighted = tab.Queue == CurrentQueue;
				ButtonWidget.DrawBackground(TabButton, rect, false, false, hover, highlighted);
				contentWidth.Value += TabWidth - 1;

				var textSize = font.Value!.Measure(tab.Name);
				var position = new int2(rect.X + (rect.Width - textSize.X) / 2, rect.Y + (rect.Height - textSize.Y) / 2);
				font.Value.DrawTextWithContrast(tab.Name, position, tab.Queue.AllQueued().Any(i => i.Done) ? Color.Gold : Color.White, Color.Black, 1);
			}

			Game.Renderer.DisableScissor();
		}
	}
}
