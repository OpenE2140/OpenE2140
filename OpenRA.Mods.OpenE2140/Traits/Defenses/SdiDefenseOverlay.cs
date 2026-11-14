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
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Traits.Mcu;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Defenses;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("Renders the SDI Defense interception range circle when the building is selected.")]
public class SdiDefenseOverlayInfo : TraitInfo, Requires<InterceptsProjectilesInfo>, Requires<BuildingInfo>
{
	[Desc("Player relationships who can view the overlay.")]
	public readonly PlayerRelationship ValidRelationships = PlayerRelationship.Ally;

	[Desc("Color of the interception range circle.")]
	public readonly Color RangeColor = Color.FromArgb(0xFFB4DF83);

	public override object Create(ActorInitializer init)
	{
		return new SdiDefenseOverlay(init.Self, this);
	}
}

public class SdiDefenseOverlay : IRenderAnnotations
{
	private readonly SdiDefenseOverlayInfo info;
	private readonly InterceptsProjectilesInfo interceptsProjectilesInfo;
	private readonly ICustomBuildingInfo? customBuildingInfo;

	public SdiDefenseOverlay(Actor self, SdiDefenseOverlayInfo info)
	{
		this.info = info;
		this.interceptsProjectilesInfo = self.Info.TraitInfo<InterceptsProjectilesInfo>();
		this.customBuildingInfo = CustomBuildingInfoWrapper.WrapIfNecessary(self.Info);
	}

	bool IRenderAnnotations.SpatiallyPartitionable => true;

	IEnumerable<IRenderable> IRenderAnnotations.RenderAnnotations(Actor self, WorldRenderer wr)
	{
		if (!this.ShouldRender(self))
			return [];

		if (this.customBuildingInfo == null)
			return [];

		var footprintCenter = this.customBuildingInfo.GetCenterOfFootprint(self.Location);

		return [new CircleAnnotationRenderable(footprintCenter, this.interceptsProjectilesInfo.HorizontalRange, 1, this.info.RangeColor)];
	}

	private bool ShouldRender(Actor self)
	{
		if (self.World.FogObscures(self) || !self.World.Selection.Contains(self))
			return false;

		var viewer = self.World.RenderPlayer ?? self.World.LocalPlayer;

		if (viewer != null)
		{
			if (!this.info.ValidRelationships.HasRelationship(self.Owner.RelationshipWith(viewer)))
				return false;
		}

		return true;
	}
}
