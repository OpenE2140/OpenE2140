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

using OpenRA.Effects;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Traits;

namespace OpenRA.Mods.OpenE2140.Effects
{
	public class CustomRallyPointIndicator : IEffect, IEffectAboveShroud, IEffectAnnotation
	{
		private readonly Actor building;
		private readonly CustomRallyPoint rp;
		private readonly Animation? flag;
		private readonly Animation? circles;
		private readonly List<WPos> targetLineNodes = [];
		private readonly List<CPos> cachedLocations = [];

		public CustomRallyPointIndicator(Actor building, CustomRallyPoint rp)
		{
			this.building = building;
			this.rp = rp;

			if (rp.Info.Image != null)
			{
				this.flag = new Animation(building.World, rp.Info.Image);
				this.flag.PlayRepeating(rp.Info.FlagSequence);

				this.circles = new Animation(building.World, rp.Info.Image);
				this.circles.Play(rp.Info.CirclesSequence);
			}

			this.UpdateTargetLineNodes(building.World);
		}

		void IEffect.Tick(World world)
		{
			this.flag?.Tick();

			this.circles?.Tick();

			if (this.cachedLocations == null || !this.cachedLocations.SequenceEqual(this.rp.Path))
			{
				this.UpdateTargetLineNodes(world);

				this.circles?.Play(this.rp.Info.CirclesSequence);
			}

			if (!this.building.IsInWorld || this.building.IsDead)
				world.AddFrameEndTask(w => w.Remove(this));
		}

		private void UpdateTargetLineNodes(World world)
		{
			this.cachedLocations.Clear();
			this.cachedLocations.AddRange(this.rp.Path);
			this.targetLineNodes.Clear();

			foreach (var c in this.cachedLocations)
				this.targetLineNodes.Add(world.Map.CenterOfCell(c));

			if (this.targetLineNodes.Count == 0)
				return;

			var exit = this.building.NearestExitOrDefault(this.targetLineNodes[0]);
			this.targetLineNodes.Insert(0, this.building.CenterPosition + (this.rp.Info.LineInitialOffset ?? exit?.Info?.SpawnOffset ?? WVec.Zero));
		}

		IEnumerable<IRenderable> IEffect.Render(WorldRenderer wr) { return SpriteRenderable.None; }

		IEnumerable<IRenderable> IEffectAboveShroud.RenderAboveShroud(WorldRenderer wr)
		{
			if (!this.building.IsInWorld || !this.building.Owner.IsAlliedWith(this.building.World.LocalPlayer))
				return SpriteRenderable.None;

			if (!this.building.World.Selection.Contains(this.building))
				return SpriteRenderable.None;

			var renderables = SpriteRenderable.None;

			if (this.targetLineNodes.Count > 0 && (this.circles != null || this.flag != null))
			{
				var palette = wr.Palette(this.rp.PaletteName);

				if (this.circles != null)
					renderables = renderables.Concat(this.circles.Render(this.targetLineNodes[^1], palette));

				if (this.flag != null)
					renderables = renderables.Concat(this.flag.Render(this.targetLineNodes[^1], palette));
			}

			return renderables;
		}

		IEnumerable<IRenderable> IEffectAnnotation.RenderAnnotation(WorldRenderer wr)
		{
			if (Game.Settings.Game.TargetLines == TargetLinesType.Disabled)
				return SpriteRenderable.None;

			if (!this.building.IsInWorld || !this.building.Owner.IsAlliedWith(this.building.World.LocalPlayer))
				return SpriteRenderable.None;

			if (!this.building.World.Selection.Contains(this.building))
				return SpriteRenderable.None;

			if (this.targetLineNodes.Count == 0)
				return SpriteRenderable.None;

			return this.RenderInner();
		}

		private IEnumerable<IRenderable> RenderInner()
		{
			var prev = this.targetLineNodes[0];

			foreach (var pos in this.targetLineNodes.Skip(1))
			{
				var targetLine = new[] { prev, pos };
				prev = pos;

				yield return new TargetLineRenderable(targetLine, this.building.Owner.Color, this.rp.Info.LineWidth, 1);
			}
		}
	}
}

