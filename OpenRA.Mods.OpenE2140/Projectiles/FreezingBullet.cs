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

using OpenRA.GameRules;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Projectiles;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Projectiles;

[Desc($"Specialized type of {nameof(Bullet)} that can freeze when hitting target for a period of time.")]
public class FreezingBulletInfo : BulletInfo, IRulesetLoaded
{
	[Desc("Freezes on impact for specified amount of ticks.")]
	public readonly int FreezeForTicks;

	[Desc("When frozen, use this image instead of the default one.")]
	public readonly string? FrozenImage = null;

	[SequenceReference(nameof(FrozenImage), allowNullImage: true)]
	[Desc($"Loop a sequence of {nameof(FrozenImage)} from this list while this projectile is frozen. " +
		$"Sequence is picked based on the sequence chosen from {nameof(Sequences)}")]
	public readonly string[] FrozenSequences = { "idle" };

	public override IProjectile Create(ProjectileArgs args)
	{
		return new FreezingBullet(this, args);
	}

	public void RulesetLoaded(Ruleset rules, ActorInfo info)
	{
		if (string.IsNullOrEmpty(this.FrozenImage))
			return;

		if (string.IsNullOrEmpty(this.Image))
			throw new YamlException($"When {nameof(this.FrozenImage)} is specified, {nameof(this.Image)} has to be specified too.");

		if (this.FrozenSequences.Length != this.Sequences.Length)
			throw new YamlException($"When {nameof(this.FrozenImage)} is specified, number of sequences in {nameof(this.Sequences)} must match" +
				$"number of sequences in {nameof(this.FrozenSequences)}");
	}
}

public class FreezingBullet : Bullet
{
	private enum State { Moving, Freezed }

	private readonly FreezingBulletInfo info;
	private State state = State.Moving;
	private int frozenTicks;

	public FreezingBullet(FreezingBulletInfo info, ProjectileArgs args)
		: base(info, args)
	{
		this.info = info;
		this.frozenTicks = info.FreezeForTicks;
	}

	public override void Tick(World world)
	{
		if (this.state == State.Moving)
		{
			base.Tick(world);
			return;
		}

		this.Animation?.Tick();
		if (--this.frozenTicks <= 0)
		{
			world.AddFrameEndTask(w => w.Remove(this));
		}
	}

	protected override void Explode(World world)
	{
		if (this.frozenTicks == 0)
		{
			world.AddFrameEndTask(w => w.Remove(this));
			return;
		}	

		var warheadArgs = new WarheadArgs(this.Args)
		{
			ImpactOrientation = new WRot(WAngle.Zero, Util.GetVerticalAngle(this.lastPos, this.pos), this.Args.Facing),
			ImpactPosition = pos,
		};

		this.Args.Weapon.Impact(Target.FromPos(this.pos), warheadArgs);

		this.state = State.Freezed;

		// Change bullet's sprite sequence to a appropriate one from FrozenSequences
		var currentSequenceIndex = Array.IndexOf(this.info.Sequences, this.Animation.CurrentSequence.Name);
		if (!string.IsNullOrEmpty(this.info.FrozenImage))
			this.Animation.ChangeImage(this.info.FrozenImage, "");
		this.Animation.PlayRepeating(this.info.FrozenSequences[currentSequenceIndex]);
	}

	public override IEnumerable<OpenRA.Graphics.IRenderable> Render(OpenRA.Graphics.WorldRenderer wr)
	{
		if (!this.FlightLengthReached && this.state == State.Moving)
			return base.Render(wr);
		else
			return base.RenderAnimation(wr);
	}
}
