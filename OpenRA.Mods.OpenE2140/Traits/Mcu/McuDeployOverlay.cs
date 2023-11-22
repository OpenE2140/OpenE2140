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

public class McuDeployOverlay : ITick, ITransformsPreview
{
	public McuDeployOverlayInfo Info { get; }

	private readonly ActorInfo mcuActor;
	private readonly ActorInfo buildingActor;
	private readonly BuildingInfo buildingInfo;
	private readonly ITransformsInfo transformsInfo;

	public McuDeployOverlay(Actor self, McuDeployOverlayInfo info)
	{
		this.Info = info;

		this.mcuActor = self.Info;
		this.buildingActor = McuUtils.GetTargetBuilding(self.World, this.mcuActor)!;
		this.buildingInfo = this.buildingActor.TraitInfo<BuildingInfo>();
		this.transformsInfo = self.Info.TraitInfo<ITransformsInfo>();
	}

	void ITick.Tick(Actor self)
	{
		// Currently noop
	}

	IEnumerable<IRenderable> ITransformsPreview.RenderAboveShroud(Actor self, WorldRenderer wr)
	{
		// code will likely need to change in order to properly support preview for Mine and Water Base (or will need custom previews)

		var topLeft = self.Location + this.transformsInfo.Offset;

		var footprint = new Dictionary<CPos, PlaceBuildingCellType>();

		// TODO: resources:
		// footprint.Add(t, MakeCellType(isCloseEnough && world.IsCellBuildable(t, actorInfo, buildingInfo) && (resourceLayer == null || resourceLayer.GetResource(t).Type == null)));

		foreach (var t in this.buildingInfo.Tiles(topLeft))
		{
			footprint.Add(t, self.World.IsCellBuildable(t, this.buildingActor, this.buildingInfo, self) ? PlaceBuildingCellType.Valid : PlaceBuildingCellType.Invalid);
		}

		foreach (var r in this.RenderPlaceBuildingPreviews(self, wr, topLeft, footprint))
			yield return r;
	}

	private IEnumerable<IRenderable> RenderPlaceBuildingPreviews(Actor self, WorldRenderer wr, CPos topLeft, Dictionary<CPos, PlaceBuildingCellType> footprint)
	{
		var previewGeneratorInfos = this.mcuActor.TraitInfos<IPlaceBuildingPreviewGeneratorInfo>();
		if (previewGeneratorInfos.Any())
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
}
