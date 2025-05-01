using JetBrains.Annotations;
using OpenRA.GameRules;
using OpenRA.Mods.Common.Effects;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Warheads;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("Create sprite effect on warhead impact.")]
public class CreateSmokeEffectWarhead : EffectWarhead
{
	[FieldLoader.Require]
	[Desc("The time between individual particle creation. Two values mean actual lifetime will vary between them.")]
	public readonly int[] Lifetime = [];

	[Desc("Randomised offset for the particle emitter.")]
	public readonly WVec Offset = WVec.Zero;

	[Desc("Randomized particle forward movement.")]
	public readonly WDist[] Speed = [WDist.Zero];

	[Desc("Randomized particle gravity.")]
	public readonly WDist[] Gravity = [WDist.Zero];

	[Desc("Randomize particle facing.")]
	public readonly bool RandomFacing = true;

	[Desc("Randomize particle turnrate.")]
	public readonly int TurnRate;

	[Desc("The rate at which particle movement properties are reset.")]
	public readonly int RandomRate = 4;

	[Desc("Which image to use.")]
	public readonly string Image = "smoke";

	[Desc("Which sequence to use.")]
	[SequenceReference(nameof(CreateSmokeEffectWarhead.Image))]
	public readonly string[] Sequences = ["particles"];

	[Desc("Which palette to use.")]
	[PaletteReference(nameof(CreateSmokeEffectWarhead.IsPlayerPalette))]
	public readonly string Palette = "effect";

	public readonly bool IsPlayerPalette;

	public override void DoImpact(in Target target, WarheadArgs args)
	{
		var firedBy = args.SourceActor;
		var pos = new WPos(target.CenterPosition.X, target.CenterPosition.Y, Math.Max(0, target.CenterPosition.Z));
		var world = firedBy.World;

		if (!this.IsValidAgainst(target, firedBy))
			return;

		var facing = firedBy.GetTraitOrDefault<IFacing>();
		var offset = this.Offset;
		var spawnFacing = !this.RandomFacing && facing != null ? facing.Facing : WAngle.FromFacing(world.LocalRandom.Next(256));

		world.AddFrameEndTask(
			w => w.Add(
				new FloatingSprite(
					firedBy,
					this.Image,
					this.Sequences,
					this.Palette,
					this.IsPlayerPalette,
					this.Lifetime,
					this.Speed,
					this.Gravity,
					this.TurnRate,
					this.RandomRate,
					pos + offset,
					spawnFacing
				)
			)
		);
	}
}
