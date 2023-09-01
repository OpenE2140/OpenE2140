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

using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.OpenE2140.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Rendering;

[Desc("Clones the actor's turret sprite with another palette below it.")]
public class WithTurretShadowInfo : ConditionalTraitInfo, Requires<RenderSpritesInfo>, IRenderActorPreviewSpritesInfo
{
	[Desc("Color to draw shadow.")]
	public readonly Color ShadowColor = Color.FromArgb(140, 0, 0, 0);

	[Desc("Shadow position offset relative to actor position (ground level).")]
	public readonly WVec Offset = WVec.Zero;

	[Desc("Shadow Z offset relative to actor sprite.")]
	public readonly int ZOffset = -5;

	[Desc("Turret names.")]
	public readonly string[] Turrets = { "primary" };

	public override object Create(ActorInitializer init) { return new WithTurretShadow(this); }

	IEnumerable<IActorPreview> IRenderActorPreviewSpritesInfo.RenderPreviewSprites(ActorPreviewInitializer init, string image, int facings, PaletteReference p)
	{
		if (!this.EnabledByDefault)
			yield break;

		foreach (var turret in this.Turrets)
		{
			var t = init.Actor.TraitInfos<TurretedInfo>().First(tt => tt.Turret == turret);
			var w = init.Actor.TraitInfos<WithSpriteTurretInfo>().First(tt => tt.Turret == turret);

			var turretFacing = t.WorldFacingFromInit(init);
			var anim = new Animation(init.World, image, turretFacing);
			anim.Play(RenderSprites.NormalizeSequence(anim, init.GetDamageState(), w.Sequence));

			yield return new ModifyableSpriteActorPreview(
				anim,
				() => t.Offset + this.Offset,
				() => this.ZOffset,
				(renderable, renderUi) =>
				{
					// When rendering inside UI, we cannot render shadow, so skip entire IRenderable
					if (renderUi || renderable is not IModifyableRenderable mr)
						return null;

					return mr.WithTint(
							new float3(this.ShadowColor.R, this.ShadowColor.G, this.ShadowColor.B) / 255f,
							mr.TintModifiers | TintModifiers.ReplaceColor
						)
						.WithAlpha(this.ShadowColor.A / 255f);
				}
			);
		}
	}
}

public class WithTurretShadow : ConditionalTrait<WithTurretShadowInfo>, IRenderModifier
{
	private readonly WithTurretShadowInfo info;
	private readonly float3 shadowColor;
	private readonly float shadowAlpha;

	public WithTurretShadow(WithTurretShadowInfo info)
		: base(info)
	{
		this.info = info;
		this.shadowColor = new float3(info.ShadowColor.R, info.ShadowColor.G, info.ShadowColor.B) / 255f;
		this.shadowAlpha = info.ShadowColor.A / 255f;
	}

	IEnumerable<IRenderable> IRenderModifier.ModifyRender(Actor self, WorldRenderer wr, IEnumerable<IRenderable> r)
	{
		if (this.IsTraitDisabled || self.World.FogObscures(self) || self.World.ShroudObscures(self.CenterPosition))
			return r;

		var height = self.World.Map.DistanceAboveTerrain(self.CenterPosition).Length;

		var shadowSprites = self.TraitsImplementing<WithMovingSpriteTurret>()
			.SelectMany(
				withMovingSpriteTurret => withMovingSpriteTurret.DefaultAnimation.Render(
					self.CenterPosition + withMovingSpriteTurret.GetTurretOffset(self),
					null
				)
			)
			.OfType<IModifyableRenderable>()
			.Select(
				modifyableRenderable => modifyableRenderable.WithTint(this.shadowColor, modifyableRenderable.TintModifiers | TintModifiers.ReplaceColor)
					.WithAlpha(this.shadowAlpha)
					.OffsetBy(this.info.Offset - new WVec(0, 0, height))
					.WithZOffset(modifyableRenderable.ZOffset + height + this.info.ZOffset)
					.AsDecoration()
			);

		return shadowSprites.Concat(r);
	}

	IEnumerable<Rectangle> IRenderModifier.ModifyScreenBounds(Actor self, WorldRenderer wr, IEnumerable<Rectangle> bounds)
	{
		foreach (var r in bounds)
			yield return r;

		if (this.IsTraitDisabled)
			yield break;

		var height = self.World.Map.DistanceAboveTerrain(self.CenterPosition).Length;
		var offset = wr.ScreenPxOffset(this.info.Offset - new WVec(0, 0, height));

		foreach (var r in bounds)
			yield return new Rectangle(r.X + offset.X, r.Y + offset.Y, r.Width, r.Height);
	}
}
