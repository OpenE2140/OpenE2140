using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Traits;

namespace OpenRA.Mods.E2140.Traits.Render
{
	public class WithMuzzlePaletteSpriteTurretInfo : WithSpriteTurretInfo
	{
		public override object Create(ActorInitializer init) { return new WithMuzzlePaletteSpriteTurret(init.Self, this); }
	}

	public class WithMuzzlePaletteSpriteTurret : WithSpriteTurret, INotifyAttack
	{
		private Animation muzzle;
		private bool fire;

		public WithMuzzlePaletteSpriteTurret(Actor self, WithSpriteTurretInfo info)
			: base(self, info)
		{
			var renderSprites = self.Trait<RenderSprites>();
			var turreted = self.TraitsImplementing<Turreted>().First(tt => tt.Name == info.Turret);
			var buildComplete = !self.Info.HasTraitInfo<BuildingInfo>();

			muzzle = new Animation(self.World, renderSprites.GetImage(self), () => turreted.WorldOrientation.Yaw);
			muzzle.PlayRepeating(NormalizeSequence(self, info.Sequence));
			renderSprites.Add(new AnimationWithOffset(muzzle,
				() => TurretOffset(self),
				() => IsTraitDisabled || !buildComplete || !fire,
				p => RenderUtils.ZOffsetFromCenter(self, p, 1)), "muzzle");
		}

		public void Attacking(Actor self, in Target target, Armament a, Barrel barrel)
		{
			fire = true;

			muzzle.PlayThen(NormalizeSequence(self, Info.Sequence), () =>
			{
				CancelCustomAnimation(self);
				fire = false;
			});
		}

		public void PreparingAttack(Actor self, in Target target, Armament a, Barrel barrel)
		{
		}
	}
}
