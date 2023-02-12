using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Rendering
{
	[Desc("Clones the actor's turret sprite with another palette below it.")]
	public class WithTurretShadowInfo : ConditionalTraitInfo, Requires<RenderSpritesInfo>
	{
		[Desc("Color to draw shadow.")]
		public readonly Color ShadowColor = Color.FromArgb(140, 0, 0, 0);

		[Desc("Shadow position offset relative to actor position (ground level).")]
		public readonly WVec Offset = WVec.Zero;

		[Desc("Shadow Z offset relative to actor sprite.")]
		public readonly int ZOffset = -5;

		[Desc("Turret names.")]
		public readonly string[] Turrets = { "primary" };

		public override object Create(ActorInitializer init) { return new WithTurretShadow(this, init); }
	}

	public class WithTurretShadow : ConditionalTrait<WithTurretShadowInfo>, IRenderModifier
	{
		private readonly WithTurretShadowInfo info;
		private readonly float3 shadowColor;
		private readonly float shadowAlpha;

		public WithTurretShadow(WithTurretShadowInfo info, ActorInitializer init)
			: base(info)
		{
			this.info = info;
			this.shadowColor = new float3(info.ShadowColor.R, info.ShadowColor.G, info.ShadowColor.B) / 255f;
			this.shadowAlpha = info.ShadowColor.A / 255f;
		}

		IEnumerable<IRenderable> IRenderModifier.ModifyRender(Actor self, WorldRenderer wr, IEnumerable<IRenderable> r)
		{
			if (IsTraitDisabled)
				return r;


			var height = self.World.Map.DistanceAboveTerrain(self.CenterPosition).Length;

			var spriteTurrets = self.TraitsImplementing<WithSpriteTurret>()
				.Where(wst => info.Turrets.Contains(wst.Info.Turret))
				.ToHashSet();

			var activeAnimations = spriteTurrets
				.Where(wst => !wst.IsTraitDisabled)
				.Select(wst => wst.DefaultAnimation)
				.ToHashSet();

			var helper = new RenderSpritesReflectionHelper(self.Trait<RenderSprites>());

			var anims = helper.GetVisibleAnimations().Where(a => activeAnimations.Contains(a.Animation));

			var renderables = helper.RenderAnimations(self, wr, anims);

			var shadowSprites = renderables.Where(s => !s.IsDecoration && s is IModifyableRenderable)
				.Select(ma => ((IModifyableRenderable)ma).WithTint(shadowColor, ((IModifyableRenderable)ma).TintModifiers | TintModifiers.ReplaceColor)
					.WithAlpha(shadowAlpha)
					.OffsetBy(info.Offset - new WVec(0, 0, height))
					.WithZOffset(ma.ZOffset + (height + info.ZOffset))
					.AsDecoration());

			return shadowSprites.Concat(r);
		}

		IEnumerable<Rectangle> IRenderModifier.ModifyScreenBounds(Actor self, WorldRenderer wr, IEnumerable<Rectangle> bounds)
		{
			foreach (var r in bounds)
				yield return r;

			if (IsTraitDisabled)
				yield break;

			var height = self.World.Map.DistanceAboveTerrain(self.CenterPosition).Length;
			var offset = wr.ScreenPxOffset(info.Offset - new WVec(0, 0, height));
			foreach (var r in bounds)
				yield return new Rectangle(r.X + offset.X, r.Y + offset.Y, r.Width, r.Height);
		}
	}
}
