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

using System.Collections;
using System.Reflection;
using JetBrains.Annotations;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Weather;

[Desc("A Day/Night cycle for the world.")]
[TraitLocation(SystemActors.World)]
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class DayNightInfo : TraitInfo, Requires<TerrainLightingInfo>, Requires<TerrainRendererInfo>
{
	[Desc("The length of a day, in ticks. Set to 0 to disable cycle and freeze the map to a specific time.")]
	public readonly int DayLength = 5000;

	[Desc("The time of the day when the game starts, in hours.")]
	public readonly float Start = 7;

	[Desc("The time of the day when the sun starts to rise, in hours.")]
	public readonly float Sunrise = 6;

	[Desc("The time of the day when the sun starts to set, in hours.")]
	public readonly float Sunset = 21;

	[Desc("The duration of the day/night transition, in hours.")]
	public readonly float DuskDuration = 1;

	[Desc("The day / sun color.")]
	public readonly Color DayColor = Color.FromArgb(255, 255, 255);

	[Desc("The night / moon color.")]
	public readonly Color NightColor = Color.FromArgb(96, 96, 192);

	[Desc("The transition / dusk color.")]
	public readonly Color DuskColor = Color.FromArgb(192, 96, 96);

	public override object Create(ActorInitializer init)
	{
		return new DayNight(this, init.Self);
	}
}

public class DayNight : ITick, IWorldLoaded
{
	private const BindingFlags BindingFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

	private readonly DayNightInfo info;

	private readonly TerrainLighting terrainLighting;
	private readonly TerrainRenderer terrainRenderer;

	private FieldInfo? globalTint;
	private FieldInfo? spriteLayer;

	private TerrainSpriteLayer? terrainSpriteLayer;

	private MethodInfo? updateTint;

	public DayNight(DayNightInfo info, Actor self)
	{
		this.info = info;

		this.terrainLighting = self.Trait<TerrainLighting>();
		this.terrainRenderer = self.Trait<TerrainRenderer>();
	}

	void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
	{
		this.globalTint = this.terrainLighting.GetType().GetField("globalTint", DayNight.BindingFlags);
		this.spriteLayer = this.terrainRenderer.GetType().GetField("spriteLayer", DayNight.BindingFlags);

		this.terrainSpriteLayer = this.spriteLayer?.GetValue(this.terrainRenderer) as TerrainSpriteLayer;

		this.updateTint = this.terrainSpriteLayer?.GetType().GetMethod("UpdateTint", DayNight.BindingFlags);
	}

	public float GetNightProgress(World world)
	{
		var timeOfDay = ((this.info.DayLength == 0 ? 0 : world.WorldTick * 24f / this.info.DayLength) + this.info.Start) % 24;

		if (timeOfDay < this.info.Sunrise)
			return 1;

		if (timeOfDay < this.info.Sunrise + this.info.DuskDuration)
			return 1 - (timeOfDay - this.info.Sunrise) / this.info.DuskDuration;

		if (timeOfDay < this.info.Sunset)
			return 0;

		if (timeOfDay < this.info.Sunset + this.info.DuskDuration)
			return (timeOfDay - this.info.Sunset) / this.info.DuskDuration;

		return 1;
	}

	void ITick.Tick(Actor self)
	{
		var light = this.GetLightColor(self.World);

		this.globalTint?.SetValue(this.terrainLighting, new float3(light.R, light.G, light.B) / byte.MaxValue);

		foreach (var cell in self.World.Map.AllCells)
			this.updateTint?.Invoke(this.terrainSpriteLayer, new object[] { cell.ToMPos(self.World.Map) });
	}

	private Color GetLightColor(World world)
	{
		var progress = this.GetNightProgress(world);

		return progress < 0.5
			? this.info.DayColor.Lerp(this.info.DuskColor, progress * 2)
			: this.info.NightColor.Lerp(this.info.DuskColor, 1 - (progress - 0.5f) * 2);
	}
}
