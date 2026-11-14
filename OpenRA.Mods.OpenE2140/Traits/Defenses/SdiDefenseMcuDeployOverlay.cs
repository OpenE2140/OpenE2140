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
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.OpenE2140.Traits.Mcu;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Defenses;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("Renders the SDI Defense interception range circle during MCU deployment preview.")]
public class SdiDefenseMcuDeployOverlayInfo : TraitInfo<SdiDefenseMcuDeployOverlayInfo>, Requires<McuInfo>
{
	[Desc("Color of the range circle when the building can be deployed at the hovered location.")]
	public readonly Color ValidColor = Color.Green;

	[Desc("Color of the range circle when the building cannot be deployed at the hovered location.")]
	public readonly Color InvalidColor = Color.Red;

	public override object Create(ActorInitializer init)
	{
		return new SdiDefenseMcuDeployOverlay(this);
	}
}

public class SdiDefenseMcuDeployOverlay : ICustomMcuDeployOverlayGenerator
{
	private readonly SdiDefenseMcuDeployOverlayInfo info;

	public SdiDefenseMcuDeployOverlay(SdiDefenseMcuDeployOverlayInfo info)
	{
		this.info = info;
	}

	ICustomMcuDeployOverlay ICustomMcuDeployOverlayGenerator.CreateOverlay(Actor self, WorldRenderer wr, ActorInfo _)
	{
		return new SdiDefenseMcuDeployOverlayRenderer(self, this.info);
	}
}

public class SdiDefenseMcuDeployOverlayRenderer : ICustomMcuDeployOverlay
{
	private readonly SdiDefenseMcuDeployOverlayInfo info;
	private readonly ICustomBuildingInfo customBuildingInfo;
	private readonly InterceptsProjectilesInfo interceptsProjectilesInfo;

	public SdiDefenseMcuDeployOverlayRenderer(Actor self, SdiDefenseMcuDeployOverlayInfo info)
	{
		this.info = info;

		var buildingActor = McuUtils.GetTargetBuilding(self.World, self.Info)
			?? throw new InvalidOperationException($"MCU '{self.Info.Name}' does not transform into a building.");

		this.customBuildingInfo = CustomBuildingInfoWrapper.WrapIfNecessary(buildingActor)
			?? throw new InvalidOperationException($"Building '{buildingActor.Name}' does not have a usable Building trait.");

		this.interceptsProjectilesInfo = buildingActor.TraitInfo<InterceptsProjectilesInfo>();
	}

	IEnumerable<IRenderable> ICustomMcuDeployOverlay.Render(Actor self, WorldRenderer wr, CPos topLeft, Dictionary<CPos, PlaceBuildingCellType> footprint)
	{
		return [];
	}

	IEnumerable<IRenderable> ICustomMcuDeployOverlay.RenderAnnotations(Actor self, WorldRenderer wr, CPos topLeft, Dictionary<CPos, PlaceBuildingCellType> footprint)
	{
		var footprintCenter = this.customBuildingInfo.GetCenterOfFootprint(topLeft);
		var color = footprint.Values.All(c => c == PlaceBuildingCellType.Valid) ? this.info.ValidColor : this.info.InvalidColor;

		yield return new CircleAnnotationRenderable(footprintCenter, this.interceptsProjectilesInfo.HorizontalRange, 1, color);
	}
}
