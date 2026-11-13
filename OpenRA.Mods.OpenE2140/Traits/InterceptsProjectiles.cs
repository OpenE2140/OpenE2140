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

using System.Numerics;
using JetBrains.Annotations;
using OpenRA.Effects;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("Intercepts interceptable projectiles within range with a delayed laser beam.")]
public class InterceptsProjectilesInfo : PausableConditionalTraitInfo
{
	[Desc("Maximum horizontal range to search for interceptable projectiles.")]
	public readonly WDist HorizontalRange = WDist.FromCells(5);

	[Desc("Maximum altitude above terrain the beam can reach.")]
	public readonly WDist VerticalRange = WDist.FromCells(3);

	[Desc("Ticks between successful interceptions.")]
	public readonly int Cooldown = 30;

	[Desc("Ticks from lock-on until the projectile is destroyed.")]
	public readonly int InterceptDelay = 5;

	[Desc("Offsets from the actor's center position where the beams start.")]
	public readonly WVec[] Offsets = [WVec.Zero, WVec.Zero, WVec.Zero];

	[Desc("Distance from the actor's center where the beams merge.")]
	public readonly WDist MergeDistance = WDist.FromCells(2);

	[Desc("What player relationships can be targeted.")]
	public readonly PlayerRelationship ValidRelationships = PlayerRelationship.Enemy;

	[Desc("Width of the beam.")]
	public readonly WDist Width = new(20);

	[Desc("Shape of the beam.")]
	public readonly BeamRenderableShape Shape = BeamRenderableShape.Cylindrical;

	[Desc("Color of the beam.")]
	public readonly Color Color = Color.LightGreen;

	[Desc("Draw a second beam for a glow effect.")]
	public readonly bool SecondaryBeam = true;

	[Desc("Width of the secondary beam.")]
	public readonly WDist SecondaryBeamWidth = new(35);

	[Desc("Color of the secondary beam.")]
	public readonly Color SecondaryBeamColor = Color.LightGreen;

	[Desc("Z offset for beam rendering.")]
	public readonly int ZOffset = 2048;

	[Desc("Sound to play when a target projectile is locked.")]
	public readonly string? Sound;

	public override object Create(ActorInitializer init) { return new InterceptsProjectiles(init.Self, this); }
}

public class InterceptsProjectiles : PausableConditionalTrait<InterceptsProjectilesInfo>, ITick
{
	private readonly Actor selfActor;

	private IInterceptableProjectile? target;
	private InterceptBeam? beam;
	private int cooldown;
	private int delayTicks;

	public InterceptsProjectiles(Actor self, InterceptsProjectilesInfo info)
		: base(info)
	{
		this.selfActor = self;
	}

	void ITick.Tick(Actor self)
	{
		if (this.IsTraitDisabled || this.IsTraitPaused || self.IsDead)
		{
			this.Cancel();
			return;
		}

		if (this.cooldown > 0)
			this.cooldown--;

		if (this.target != null)
		{
			if (!self.World.Effects.OfType<IInterceptableProjectile>().Contains(this.target))
			{
				this.Cancel();
				return;
			}

			if (--this.delayTicks <= 0)
			{
				this.target.Intercept(self.World, self);
				this.cooldown = this.Info.Cooldown;
				this.Cancel();
			}

			return;
		}

		if (this.cooldown > 0)
			return;

		// TODO: improve perf by implementing EffectMap (similar to ScreenMap; needs more engine changes)

		var source = self.CenterPosition + (this.Info.Offsets.Length > 0 ? this.Info.Offsets[0] : WVec.Zero);
		this.target = self.World.Effects
			.OfType<IInterceptableProjectile>()
			.Where(p => this.Info.ValidRelationships.HasRelationship(self.Owner.RelationshipWith(p.Owner)))
			.Where(p => (p.Position - source).HorizontalLengthSquared <= this.Info.HorizontalRange.LengthSquared)
			.Where(p => self.World.Map.DistanceAboveTerrain(p.Position) <= this.Info.VerticalRange)
			.OrderBy(p => (p.Position - source).HorizontalLengthSquared)
			.ThenBy(p => p.Position.X)
			.ThenBy(p => p.Position.Y)
			.FirstOrDefault();

		if (this.target == null)
			return;

		this.delayTicks = this.Info.InterceptDelay;
		this.beam = new InterceptBeam(self, this.Info, this.target);

		if (!string.IsNullOrEmpty(this.Info.Sound))
			Game.Sound.Play(SoundType.World, this.Info.Sound, this.target.Position);

		self.World.AddFrameEndTask(w => w.Add(this.beam));
	}

	private void Cancel()
	{
		this.target = null;
		if (this.beam == null)
			return;

		var b = this.beam;
		this.beam = null;
		this.selfActor.World.AddFrameEndTask(w => w.Remove(b));
	}

	private static WPos CalculateMergePoint(WPos[] sources, WPos center, WPos target, int distance)
	{
		if (sources.Length < 3)
			return CenterLineMergePoint(center, target, distance);

		var a = ToVector3(sources[0]);
		var b = ToVector3(sources[1]);
		var c = ToVector3(sources[2]);
		var targetV = ToVector3(target);
		var centerV = ToVector3(center);

		var ab = b - a;
		var ac = c - a;
		var n = Vector3.Cross(ab, ac);

		if (n.LengthSquared() < 0.0001f)
			return CenterLineMergePoint(center, target, distance);

		if (Vector3.Dot(n, targetV - a) < 0)
			n = -n;

		var abLenSq = ab.LengthSquared();
		var acLenSq = ac.LengthSquared();
		var nLenSq = n.LengthSquared();

		var circumcenter = a
			+ (Vector3.Cross(ac, n) * abLenSq + Vector3.Cross(n, ab) * acLenSq)
			/ (2 * nLenSq);

		var u = circumcenter - centerV;
		var uDotN = Vector3.Dot(u, n);
		var uLenSq = u.LengthSquared();
		var d2 = distance * distance;

		var aQuad = nLenSq;
		var bQuad = 2 * uDotN;
		var cQuad = uLenSq - d2;

		var discriminant = bQuad * bQuad - 4 * aQuad * cQuad;
		float t;

		if (discriminant >= 0)
		{
			var sqrt = MathF.Sqrt(discriminant);
			var t1 = (-bQuad + sqrt) / (2 * aQuad);
			var t2 = (-bQuad - sqrt) / (2 * aQuad);

			var merge1 = circumcenter + n * t1;
			var merge2 = circumcenter + n * t2;
			var toTarget = targetV - centerV;

			t = Vector3.Dot(merge1 - centerV, toTarget) > Vector3.Dot(merge2 - centerV, toTarget) ? t1 : t2;
		}
		else
		{
			return CenterLineMergePoint(center, target, distance);
		}

		var merge = circumcenter + n * t;
		return new WPos((int)merge.X, (int)merge.Y, (int)merge.Z);
	}

	private static WPos CenterLineMergePoint(WPos center, WPos target, int distance)
	{
		var dir = target - center;
		if (dir.Length == 0)
			return target;

		var d = distance < dir.Length ? distance : dir.Length;
		return center + dir * d / dir.Length;
	}

	private static Vector3 ToVector3(WPos pos) => new(pos.X, pos.Y, pos.Z);

	private sealed class InterceptBeam : IEffect
	{
		private readonly Actor sourceActor;
		private readonly InterceptsProjectilesInfo info;
		private readonly IInterceptableProjectile target;

		public InterceptBeam(Actor sourceActor, InterceptsProjectilesInfo info, IInterceptableProjectile target)
		{
			this.sourceActor = sourceActor;
			this.info = info;
			this.target = target;
		}

		public void Tick(OpenRA.World world) { }

		public IEnumerable<IRenderable> Render(WorldRenderer wr)
		{
			var basePos = this.sourceActor.CenterPosition;
			var targetPos = this.target.Position;

			var sources = this.info.Offsets.Select(o => basePos + o).ToArray();

			var mergePoint = sources.Length >= 3
				? InterceptsProjectiles.CalculateMergePoint(sources, basePos, targetPos, this.info.MergeDistance.Length)
				: targetPos;

			foreach (var source in sources)
			{
				var length = mergePoint - source;
				yield return new BeamRenderable(source, this.info.ZOffset, length, this.info.Shape, this.info.Width, this.info.Color);

				if (this.info.SecondaryBeam)
					yield return new BeamRenderable(source, this.info.ZOffset, length, this.info.Shape, this.info.SecondaryBeamWidth, this.info.SecondaryBeamColor);
			}

			var trunkLength = targetPos - mergePoint;
			yield return new BeamRenderable(mergePoint, this.info.ZOffset, trunkLength, this.info.Shape, this.info.Width, this.info.Color);

			if (this.info.SecondaryBeam)
				yield return new BeamRenderable(mergePoint, this.info.ZOffset, trunkLength, this.info.Shape, this.info.SecondaryBeamWidth, this.info.SecondaryBeamColor);
		}
	}
}
