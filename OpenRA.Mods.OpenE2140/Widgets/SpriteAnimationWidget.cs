using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.OpenE2140.Widgets;

public class SpriteAnimationWidget : Widget
{
	private int currentTick;
	private int currentFrame;
	private Sprite[] spritesheet = [];

	public int TickLength = 1;

	public SpriteAnimationWidget() { }

	protected SpriteAnimationWidget(SpriteAnimationWidget other)
		: base(other)
	{
	}

	public void SetSpriteSheet(Sprite[] spritesheet)
	{
		this.spritesheet = spritesheet;

		this.currentFrame = -1;
		this.currentTick = this.TickLength;
	}

	public override SpriteAnimationWidget Clone() { return new SpriteAnimationWidget(this); }

	public override void Tick()
	{
		if (--this.currentTick > 0)
			return;

		this.currentFrame = (this.currentFrame + 1) % this.spritesheet.Length;
		this.currentTick = this.TickLength;
	}

	public override void Draw()
	{
		if (this.spritesheet.Length == 0 || this.currentFrame >= this.spritesheet.Length || this.currentFrame < 0)
			return;

		var sprite = this.spritesheet[this.currentFrame];
		if (sprite != null)
		{
			var x = (this.RenderBounds.Width - sprite.Size.X) / 2 + this.RenderBounds.X;
			var y = (this.RenderBounds.Height - sprite.Size.Y) / 2 + this.RenderBounds.Y;
			WidgetUtils.DrawSprite(sprite, new float2(x, y));
		}
	}
}
