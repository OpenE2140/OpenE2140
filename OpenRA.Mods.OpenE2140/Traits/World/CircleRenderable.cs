using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Mods.OpenE2140.Traits.World;

public class CircleRenderable : IRenderable, IFinalizedRenderable
{
	private const int CircleSegments = 32;
	private static readonly WVec[] FacingOffsets = Exts.MakeArray(CircleSegments, i => new WVec(1024, 0, 0).Rotate(WRot.FromFacing(i * 256 / CircleSegments)));
	private readonly WDist radius;
	private readonly int width;
	private readonly Color color;
	private readonly bool filled;

	public CircleRenderable(WPos centerPosition, WDist radius, int width, Color color, bool filled = false)
	{
		this.Pos = centerPosition;
		this.radius = radius;
		this.width = width;
		this.color = color;
		this.filled = filled;
	}

	public WPos Pos { get; }
	public int ZOffset => 0;
	public bool IsDecoration => true;

	public IRenderable WithZOffset(int newOffset) { return new CircleRenderable(this.Pos, this.radius, this.width, this.color, this.filled); }
	public IRenderable OffsetBy(in WVec vec) { return new CircleRenderable(this.Pos + vec, this.radius, this.width, this.color, this.filled); }
	public IRenderable AsDecoration() { return this; }

	public IFinalizedRenderable PrepareRender(WorldRenderer wr) { return this; }
	public void Render(WorldRenderer wr)
	{
		var cr = Game.Renderer.WorldRgbaColorRenderer;
		if (this.filled)
		{
			var offset = new WVec(this.radius.Length, this.radius.Length, 0);
			var tl = wr.ScreenPxPosition(this.Pos - offset);
			var br = wr.ScreenPxPosition(this.Pos + offset);

			cr.FillEllipse(tl, br, this.color);
		}
		else
		{
			var r = this.radius.Length;
			var a = wr.ScreenPxPosition(this.Pos + r * FacingOffsets[CircleSegments - 1] / 1024);
			for (var i = 0; i < CircleSegments; i++)
			{
				var b = wr.ScreenPxPosition(this.Pos + r * FacingOffsets[i] / 1024);
				cr.DrawLine(a, b, this.width, this.color);
				a = b;
			}
		}
	}

	public void RenderDebugGeometry(WorldRenderer wr) { }
	public Rectangle ScreenBounds(WorldRenderer wr) { return Rectangle.Empty; }
}
