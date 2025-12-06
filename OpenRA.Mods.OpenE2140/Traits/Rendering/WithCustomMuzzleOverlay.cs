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
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Rendering;

[Desc("Renders the MuzzleSequence from the Armament trait with zero offset.")]
public class WithCustomMuzzleOverlayInfo : ConditionalTraitInfo, Requires<RenderSpritesInfo>, Requires<AttackBaseInfo>, Requires<ArmamentInfo>
{
	[Desc(
		"Draw relative to the weapon's position but with zero offset. "
		+ "This means the rendering is same as with original WithMuzzleOverlay trait just Armament's LocalOffset is 0,0,0. "
		+ "Useful when muzzle sprites in assets are properly aligned with sprites having the actor's weapon. "
		+ "When set to false, original behavior of WithMuzzleOverlay trait is used"
	)]
	public readonly bool ZeroOffset;

	public override object Create(ActorInitializer init) { return new WithCustomMuzzleOverlay(init.Self, this); }
}

public class WithCustomMuzzleOverlay : ConditionalTrait<WithCustomMuzzleOverlayInfo>, INotifyAttack, IRender, ITick
{
	private readonly Dictionary<Barrel, bool> visible = [];
	private readonly Dictionary<Barrel, AnimationWithOffset> anims = [];
	private readonly Armament[] armaments;

	public WithCustomMuzzleOverlay(Actor self, WithCustomMuzzleOverlayInfo info)
		: base(info)
	{
		var render = self.Trait<RenderSprites>();
		var facing = self.TraitOrDefault<IFacing>();

		this.armaments = self.TraitsImplementing<Armament>().Where(arm => arm.Info.MuzzleSequence != null).ToArray();

		foreach (var arm in this.armaments)
		{
			foreach (var b in arm.Barrels)
			{
				var barrel = b;
				var turreted = self.TraitsImplementing<Turreted>().FirstOrDefault(t => t.Name == arm.Info.Turret);

				Func<WAngle> getFacing;

				if (turreted != null)
					getFacing = () => turreted.WorldOrientation.Yaw;
				else if (facing != null)
					getFacing = () => facing.Facing;
				else
					getFacing = () => WAngle.Zero;

				var muzzleFlash = new Animation(self.World, render.GetImage(self), getFacing) { IsDecoration = true };

				var dummyBarrel = new Barrel { Offset = WVec.Zero, Yaw = barrel.Yaw };

				if (!this.Info.ZeroOffset)
					dummyBarrel = barrel;

				this.visible.Add(barrel, false);

				this.anims.Add(
					barrel,
					new AnimationWithOffset(
						muzzleFlash,
						() => arm.MuzzleOffset(self, dummyBarrel),
						() => this.IsTraitDisabled || !this.visible[barrel],
						p => RenderUtils.ZOffsetFromCenter(self, p, 2)
					)
				);
			}
		}
	}

	void INotifyAttack.Attacking(Actor self, in Target target, Armament armement, Barrel barrel)
	{
		if (armement == null || barrel == null || !this.armaments.Contains(armement))
			return;

		var sequence = armement.Info.MuzzleSequence;
		this.visible[barrel] = true;
		this.anims[barrel].Animation.PlayThen(sequence, () => this.visible[barrel] = false);
	}

	void INotifyAttack.PreparingAttack(Actor self, in Target target, Armament armament, Barrel barrel) { }

	IEnumerable<IRenderable> IRender.Render(Actor self, WorldRenderer wr)
	{
		foreach (var arm in this.armaments)
		{
			var palette = wr.Palette(arm.Info.MuzzlePalette);

			foreach (var barrel in arm.Barrels)
			{
				var anim = this.anims[barrel];

				if (anim.DisableFunc != null && anim.DisableFunc())
					continue;

				foreach (var r in anim.Render(self, palette))
					yield return r;
			}
		}
	}

	IEnumerable<Rectangle> IRender.ScreenBounds(Actor self, WorldRenderer wr)
	{
		// Muzzle flashes don't contribute to actor bounds
		yield break;
	}

	void ITick.Tick(Actor self)
	{
		foreach (var a in this.anims.Values)
			a.Animation.Tick();
	}
}
