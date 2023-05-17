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

using JetBrains.Annotations;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Traits.Weather;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Rendering;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("Adds a localized circular light centered on the actor.")]
public class WithNightLightSourceInfo : ConditionalTraitInfo
{
	[Desc("Light range.")]
	public readonly WDist Range = WDist.FromCells(3);

	[Desc("Light intensity.")]
	public readonly float Intensity = .5f;

	[Desc("Light Color.")]
	public readonly Color Color = Color.FromArgb(64, 64, 64);

	[Desc("Position relative to body")]
	public readonly WVec Offset = WVec.Zero;

	public override object Create(ActorInitializer init) { return new WithNightLightSource(init.Self, this); }
}

public sealed class WithNightLightSource : ConditionalTrait<WithNightLightSourceInfo>, INotifyRemovedFromWorld, ITick
{
	private readonly TerrainLighting terrainLighting;
	private readonly DayNight? dayNight;

	private int lightingToken = -1;

	public WithNightLightSource(Actor self, WithNightLightSourceInfo info)
		: base(info)
	{
		this.terrainLighting = self.World.WorldActor.Trait<TerrainLighting>();
		this.dayNight = self.World.WorldActor.TraitOrDefault<DayNight>();
	}

	void INotifyRemovedFromWorld.RemovedFromWorld(Actor self)
	{
		this.Remove();
	}

	protected override void TraitEnabled(Actor self)
	{
		this.Add(self);
	}

	protected override void TraitDisabled(Actor self)
	{
		this.Remove();
	}

	void ITick.Tick(Actor self)
	{
		if (this.dayNight == null || this.IsTraitDisabled)
			return;

		var progress = this.dayNight.GetNightProgress(self.World);

		if (progress < 0.5)
			this.Remove();
		else
			this.Add(self);
	}

	private void Add(Actor self)
	{
		if (this.lightingToken != -1)
			return;

		this.lightingToken = this.terrainLighting.AddLightSource(
			self.CenterPosition + this.Info.Offset,
			this.Info.Range,
			this.Info.Intensity,
			new float3(this.Info.Color.R, this.Info.Color.G, this.Info.Color.B) / byte.MaxValue
		);
	}

	private void Remove()
	{
		if (this.lightingToken == -1)
			return;

		this.terrainLighting.RemoveLightSource(this.lightingToken);
		this.lightingToken = -1;
	}
}
