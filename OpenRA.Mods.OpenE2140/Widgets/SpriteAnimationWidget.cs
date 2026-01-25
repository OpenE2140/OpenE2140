using System.Diagnostics;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.OpenE2140.Widgets;

public class SpriteAnimationWidget : Widget
{
	private readonly Stopwatch playTime = new();

	private int currentFrame;
	private int framerate;
	private float invLength;
	private Sprite[] spritesheet = [];


	public SpriteAnimationWidget() { }

	protected SpriteAnimationWidget(SpriteAnimationWidget other)
		: base(other)
	{
	}

	public void SetSpriteSheet(Sprite[] spritesheet, int framerate = 35)
	{
		this.spritesheet = spritesheet;

		this.currentFrame = 0;
		this.framerate = Math.Clamp(framerate, 1, 200);
		this.invLength = this.framerate * 1f / this.spritesheet.Length;
		this.playTime.Restart();
	}

	public override SpriteAnimationWidget Clone() { return new SpriteAnimationWidget(this); }

	public override void Draw()
	{
		if (this.spritesheet.Length == 0)
			return;

		this.currentFrame = (int)float2.Lerp(0, this.spritesheet.Length, (float)this.playTime.Elapsed.TotalSeconds * this.invLength);
		if (this.currentFrame >= this.spritesheet.Length)
		{
			this.currentFrame = 0;
			this.playTime.Restart();
		}

		if (this.currentFrame >= 0 && this.currentFrame < this.spritesheet.Length)
		{
			var sprite = this.spritesheet[this.currentFrame];
			if (sprite != null)
			{
				var x = (this.RenderBounds.Width - sprite.Size.X) / 2 + this.RenderBounds.X;
				var y = (this.RenderBounds.Height - sprite.Size.Y) / 2 + this.RenderBounds.Y;
				WidgetUtils.DrawSprite(sprite, new float2(x, y));
			}
		}
	}
}
