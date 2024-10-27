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
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Mods.OpenE2140.Traits.WaterBase;
using OpenRA.Primitives;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.OpenE2140.Traits.World.Editor;

[TraitLocation(SystemActors.EditorWorld)]
[Desc("Required for Water Base annotations in the editor to work. Attach this to the world actor.")]
public class WaterBaseAnnotationLayerInfo : TraitInfo, Requires<EditorActorCustomRenderLayerInfo>, Requires<EditorCursorLayerInfo>
{
	[Desc("Render Water Base <-> Dock link line, when cursor is within this distance from Water Base or Dock")]
	public readonly WDist RenderLinkLineWhenMaximumDistanceToCursor = new WDist(2048);

	[Desc("Render preview of Water Base <-> Dock link, if distance from Water Base to possible dock location is within maximum allowed distance multiplied by this coefficient")]
	public readonly float RenderLinkPreviewWhenDockLocationWithinDistanceCoefficient = 1.5f;

	[Desc("Width (in pixels) of the Water Base <-> Dock link line.")]
	public readonly int LinkLineWidth = 1;

	[Desc("Width (in pixels) of the Water Base <-> Dock link end node markers.")]
	public readonly int LinkMarkerWidth = 2;

	public override object Create(ActorInitializer init) { return new WaterBaseAnnotationLayer(this); }
}

public class WaterBaseAnnotationLayer : ICustomEditorRender, IWorldLoaded
{
	private static readonly SpriteFont AnnotationFont = Game.Renderer.Fonts["Bold"];

	private readonly WaterBaseAnnotationLayerInfo info;
	private EditorViewportControllerWidget? editorViewportControllerWidget;

	public WaterBaseAnnotationLayer(WaterBaseAnnotationLayerInfo info)
	{
		this.info = info;
	}

	void IWorldLoaded.WorldLoaded(OpenRA.World w, WorldRenderer wr)
	{
		this.editorViewportControllerWidget = Ui.Root.Get<EditorViewportControllerWidget>("MAP_EDITOR");
	}

	IEnumerable<IRenderable> ICustomEditorRender.RenderAnnotations(WorldRenderer wr, CustomRenderContext context)
	{
		var cursorActor = this.GetCurrentActor();

		var cursorPosition = wr.ProjectedPosition(wr.Viewport.ViewToWorldPx(Viewport.LastMousePos));

		// Remember all Water Bases, which are linked with a Dock. Used for Water Base annotations (see below).
		var waterBasesWithLinkedDocks = new Dictionary<string, int>();

		var dockActors = context.MapActors.Values.Where(p => p.Info.HasTraitInfo<WaterBaseDockInfo>());
		foreach (var dockActor in dockActors)
		{
			var waterBaseDockInit = dockActor.GetInitOrDefault<WaterBaseDockInit>();

			if (string.IsNullOrEmpty(waterBaseDockInit?.Value?.InternalName))
			{
				yield return new TextAnnotationRenderable(AnnotationFont, dockActor.CenterPosition, 0, Color.Red, "<no water base>");
			}
			else
			{
				if (!context.MapActors.TryGetValue(waterBaseDockInit.Value.InternalName, out var waterBase))
				{
					yield return new TextAnnotationRenderable(AnnotationFont, dockActor.CenterPosition, 0, Color.Red, "<invalid water base>");
					continue;
				}

				// "Reference" count linked docks
				waterBasesWithLinkedDocks[waterBase.ID] = waterBasesWithLinkedDocks.GetValueOrDefault(waterBase.ID, 0) + 1;

				var footprintCenter = waterBase.Info.TraitInfo<WaterBaseBuildingInfo>().GetCenterOfFootprint(waterBase.GetInitOrDefault<LocationInit>().Value);
				var transforms = WaterBaseUtils.FindWaterBaseMcuTransformsFromBuildingActor(wr.World.Map.Rules, waterBase.Info);

				// Render line between linked Water Base and Dock when:
				// - Water Base or Dock actor is selected or
				// - cursor is close one of them (but currently selected actor has to be either Water Base or Dock)
				var shouldRender = waterBase.Selected || dockActor.Selected;
				if (!shouldRender && (cursorActor?.Info.HasTraitInfo<WaterBaseDockInfo>() == true || cursorActor?.Info.HasTraitInfo<WaterBaseBuildingInfo>() == true))
				{
					var dist = this.info.RenderLinkLineWhenMaximumDistanceToCursor;
					shouldRender =
						(dockActor.CenterPosition - cursorPosition).ToWDist() <= dist
						|| (footprintCenter - cursorPosition).ToWDist() <= dist;
				}

				if (shouldRender)
				{
					var color = transforms == null || (footprintCenter - dockActor.CenterPosition).ToWDist() <= transforms.MaximumDockDistance
						? Color.DarkTurquoise : Color.Red;
					yield return new WaterBaseDockLinkRenderable(dockActor.CenterPosition, footprintCenter, this.info.LinkLineWidth, this.info.LinkMarkerWidth, color);
				}
			}
		}

		foreach (var waterBaseActor in context.MapActors.Values.Where(p => p.Info.HasTraitInfo<WaterBaseBuildingInfo>()))
		{
			if (waterBasesWithLinkedDocks.TryGetValue(waterBaseActor.ID, out var linkCount))
			{
				// When there is more than 1 dock linked to a Water place, render warning annotation
				if (linkCount > 1)
					yield return new TextAnnotationRenderable(AnnotationFont, waterBaseActor.CenterPosition, 0, Color.Red, "<multiple docks>");
			}
			else
			{
				yield return new TextAnnotationRenderable(AnnotationFont, waterBaseActor.CenterPosition, 0, Color.Red, "<no dock>");

				var transforms = WaterBaseUtils.FindWaterBaseMcuTransformsFromBuildingActor(wr.World.Map.Rules, waterBaseActor.Info);
				var waterBaseBuildingInfo = waterBaseActor.Info.TraitInfo<WaterBaseBuildingInfo>();

				// Render annotations, if user is currently placing Water Base Dock and the cursor is close to the Water Base.
				if (transforms != null
					&& cursorActor?.Info.HasTraitInfo<WaterBaseDockInfo>() == true
					&& cursorActor.TryGetInit<LocationInit>(out var possibleDockLocation))
				{
					// Location of Water Base is already shifted, i.e. Offset from WaterBaseTransformsInfo shouldn't be applied
					var footprintCenter = waterBaseBuildingInfo.GetCenterOfFootprint(waterBaseActor.GetInitOrDefault<LocationInit>().Value);

					// Render circle a little bit outside of maximum dock distance, to give user an idea, how close is to the distance limit
					var dockPosition = wr.World.Map.CenterOfCell(possibleDockLocation.Value);
					var distanceToPossibleDock = (footprintCenter - dockPosition).Length;
					if (distanceToPossibleDock <= transforms.MaximumDockDistance.Length * this.info.RenderLinkPreviewWhenDockLocationWithinDistanceCoefficient)
					{
						var color = distanceToPossibleDock <= transforms.MaximumDockDistance.Length ? Color.White : Color.Red;

						yield return new CircleAnnotationRenderable(footprintCenter, transforms.MaximumDockDistance, 1, color);
					}

					// Render possible link only within the maximum distance
					if (distanceToPossibleDock <= transforms.MaximumDockDistance.Length)
						yield return new WaterBaseDockLinkRenderable(footprintCenter, dockPosition, this.info.LinkLineWidth, this.info.LinkMarkerWidth, Color.White);
				}
			}
		}
	}

	private EditorActorPreview? GetCurrentActor()
	{
		if (this.editorViewportControllerWidget?.CurrentBrush is EditorDefaultBrush editorDefaultBrush
			&& editorDefaultBrush.Selection.Actor != null)
			return editorDefaultBrush.Selection.Actor;

		if (this.editorViewportControllerWidget?.CurrentBrush is EditorActorBrush editorActorBrush)
			return editorActorBrush.Preview;

		return null;
	}
}
