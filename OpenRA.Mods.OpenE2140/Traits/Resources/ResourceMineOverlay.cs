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
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

public enum MiningAreaBorderShape { Square, Circle, None }

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("Custom overlay for ResourceMine.")]
public class ResourceMineOverlayInfo : ConditionalTraitInfo, Requires<ResourceMineInfo>, Requires<ICustomBuildingInfo>
{
	[Desc("Player relationships who can view the overlay.")]
	public readonly PlayerRelationship ValidRelationships = PlayerRelationship.Ally;

	[Desc("Color of mining area border when Mine is not depleted.")]
	public readonly Color MiningAreaBorderColor = Color.Yellow;

	[Desc("Color of mining area border when Mine is depleted.")]
	public readonly Color DepletedMiningAreaBorderColor = Color.Red;

	[Desc("Shape of mining area border")]
	public readonly MiningAreaBorderShape MiningAreaBorderShape = MiningAreaBorderShape.Circle;

	public override object Create(ActorInitializer init)
	{
		return new ResourceMineOverlay(init.Self, this);
	}
}

public class ResourceMineOverlay : ConditionalTrait<ResourceMineOverlayInfo>, IRenderAnnotations, IRenderAboveShroud
{
	private readonly ResourceMine resourceMine;
	private readonly ICustomBuildingInfo customBuildingInfo;
	private readonly IResourceLayer resourceLayer;
	private readonly (Sprite sprite, float alpha) validCell;

	public ResourceMineOverlay(Actor self, ResourceMineOverlayInfo info)
		: base(info)
	{
		this.resourceMine = self.Trait<ResourceMine>();
		this.customBuildingInfo = self.Info.TraitInfo<ICustomBuildingInfo>();

		var sequences = self.World.Map.Sequences;
		var validSequence = sequences.GetSequence("overlay", "build-resources");

		this.resourceLayer = self.World.WorldActor.Trait<IResourceLayer>();
		this.validCell = (validSequence.GetSprite(0), validSequence.GetAlpha(0));
	}

	bool IRenderAnnotations.SpatiallyPartitionable => true;

	bool IRenderAboveShroud.SpatiallyPartitionable => true;

	IEnumerable<IRenderable> IRenderAboveShroud.RenderAboveShroud(Actor self, WorldRenderer wr)
	{
		if (!this.ShouldRender(self) || !self.World.Selection.Contains(self))
			yield break;

		foreach (var cell in this.resourceMine.CellsInMiningArea)
		{
			if (this.resourceLayer.GetResource(cell).Density == 0)
				continue;

			yield return new SpriteRenderable(
				this.validCell.sprite, wr.World.Map.CenterOfCell(cell), WVec.Zero, 0, null, 1f,
				this.validCell.alpha, float3.Ones, TintModifiers.IgnoreWorldTint, true);
		}
	}

	IEnumerable<IRenderable> IRenderAnnotations.RenderAnnotations(Actor self, WorldRenderer wr)
	{
		if (!this.ShouldRender(self))
			return Enumerable.Empty<IRenderable>();

		return this.RenderOverlay(self);
	}

	private IEnumerable<IRenderable> RenderOverlay(Actor self)
	{
		var isDepleted = this.resourceMine.IsDepleted;

		// When selected, render at full opacity, if just hovered then semi-transparent.
		var color = isDepleted ? this.Info.DepletedMiningAreaBorderColor : this.Info.MiningAreaBorderColor;
		var footprintCenter = this.customBuildingInfo.GetCenterOfFootprint(self.Location);
		var borderRange = WDist.FromCells(this.resourceMine.Info.Range) + new WDist(512);

		switch (this.Info.MiningAreaBorderShape)
		{
			case MiningAreaBorderShape.Square:
			{
				var tl = footprintCenter + new WVec(-borderRange, -borderRange, WDist.Zero);
				yield return new PolygonAnnotationRenderable(
					new[]
					{
						tl,
						footprintCenter + new WVec(borderRange, -borderRange, WDist.Zero),
						footprintCenter + new WVec(borderRange, borderRange, WDist.Zero),
						footprintCenter + new WVec(-borderRange, borderRange, WDist.Zero),
					}, tl, 1, color);
				break;
			}
			case MiningAreaBorderShape.Circle:
			{
				yield return new CircleAnnotationRenderable(footprintCenter, borderRange, 1, color);
				break;
			}
		}
	}

	private bool ShouldRender(Actor self)
	{
		if (self.World.FogObscures(self) || !self.World.Selection.Contains(self))
			return false;

		var viewer = self.World.RenderPlayer ?? self.World.LocalPlayer;

		if (viewer != null)
		{
			if (!this.Info.ValidRelationships.HasRelationship(self.Owner.RelationshipWith(viewer)))
				return false;
		}

		return true;
	}
}
