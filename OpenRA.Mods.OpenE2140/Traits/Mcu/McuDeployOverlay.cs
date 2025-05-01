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

using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Mcu;

[Desc("Renders deploy overlay when cursor is hovered over MCU. Attach to MCU actor.")]
public class McuDeployOverlayInfo : TraitInfo, Requires<ITransformsInfo>
{
	public override object Create(ActorInitializer init)
	{
		return new McuDeployOverlay(init.Self, this);
	}
}

public class McuDeployOverlay : ITransformsPreview
{
	public McuDeployOverlayInfo Info { get; }

	private readonly ActorInfo mcuActor;
	private readonly ActorInfo buildingActor;
	private readonly ICustomBuildingInfo customBuildingInfo;
	private readonly ITransformsInfo transformsInfo;

	public McuDeployOverlay(Actor self, McuDeployOverlayInfo info)
	{
		this.Info = info;

		this.mcuActor = self.Info;
		this.buildingActor = McuUtils.GetTargetBuilding(self.World, this.mcuActor)!;
		this.customBuildingInfo = CustomBuildingInfoWrapper.WrapIfNecessary(this.buildingActor)!;
		this.transformsInfo = self.Info.TraitInfo<ITransformsInfo>();
	}

	public IEnumerable<IRenderable> RenderAboveShroud(Actor self, WorldRenderer wr)
	{
		var topLeft = self.Location + this.transformsInfo.Offset;
		var footprint = this.customBuildingInfo.GetBuildingPlacementFootprint(self.World, topLeft, self);

		foreach (var r in this.RenderPlaceBuildingPreviews(self, wr, topLeft, footprint))
			yield return r;

		foreach (var r in this.RenderTransformsPreviews(self, wr, o => o.Render(self, wr, topLeft, footprint)))
			yield return r;
	}

	public IEnumerable<IRenderable> RenderAnnotations(Actor self, WorldRenderer wr)
	{
		var topLeft = self.Location + this.transformsInfo.Offset;
		var footprint = this.customBuildingInfo.GetBuildingPlacementFootprint(self.World, topLeft, self);

		//foreach (var r in this.RenderPlaceBuildingPreviews(self, wr, topLeft, footprint))
		//	yield return r;

		foreach (var r in this.RenderTransformsPreviews(self, wr, o => o.RenderAnnotations(self, wr, topLeft, footprint)))
			yield return r;
	}

	private IEnumerable<IRenderable> RenderPlaceBuildingPreviews(Actor self, WorldRenderer wr, CPos topLeft, Dictionary<CPos, PlaceBuildingCellType> footprint)
	{
		var previewGeneratorInfos = this.mcuActor.TraitInfos<IPlaceBuildingPreviewGeneratorInfo>();
		if (previewGeneratorInfos.Count > 0)
		{
			var td = new TypeDictionary()
			{
				new FactionInit(self.Owner.Faction.InternalName),
				new OwnerInit(self.Owner),
			};

			foreach (var api in this.buildingActor.TraitInfos<IActorPreviewInitInfo>())
				foreach (var o in api.ActorPreviewInits(this.buildingActor, ActorPreviewType.PlaceBuilding))
					td.Add(o);

			foreach (var gen in previewGeneratorInfos)
			{
				var preview = gen.CreatePreview(wr, this.buildingActor, td);
				foreach (var r in preview.Render(wr, topLeft, footprint))
					yield return r;
			}
		}
	}

	private IEnumerable<IRenderable> RenderTransformsPreviews(
		Actor self,
		WorldRenderer wr,
		Func<ICustomMcuDeployOverlay, IEnumerable<IRenderable>> renderFunc)
	{
		var generators = self.TraitsImplementing<ICustomMcuDeployOverlayGenerator>();
		foreach (var generator in generators)
		{
			var preview = generator.CreateOverlay(self, wr, this.buildingActor);
			foreach (var r in renderFunc(preview))
				yield return r;
		}
	}
}
