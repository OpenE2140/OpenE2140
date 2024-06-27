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
using OpenRA.Mods.Common.Orders;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits;

public interface ISafeDragNotify
{
	void SafeDragFailed(Actor self, Actor movingActor);

	void SafeDragComplete(Actor self, Actor movingActor);
}

/// <summary>
/// Hook for modifying actor init objects in <see cref="TypeDictionary"/> before the actor is created by <see cref="Production.AnimatedExitProduction"/>.
/// </summary>
public interface IProduceActorInitModifier
{
	/// <summary>
	/// This hook is called just before the actor is created and makes it possible to modify actor init objects inside <see cref="TypeDictionary"/>.
	/// </summary>
	/// <remarks>
	/// The exact location, where the is hook called, is just before invoking
	/// <see cref="Common.Traits.Production.DoProduction(Actor, ActorInfo, Common.Traits.ExitInfo, string, TypeDictionary)"/> method.
	/// It means that this method can override any changes done by this hook.
	/// </remarks>
	void ModifyActorInit(Actor self, TypeDictionary init);
}

public interface INotifyTransform
{
	void TransformCanceled(Actor self);

	void BeforeTransform(Actor self);

	void OnTransform(Actor self);

	void AfterTransform(Actor toActor);
}

[RequireExplicitImplementation]
public interface ITransformsInfo : ITraitInfoInterface
{
	string? IntoActor { get; }

	CVec Offset { get; }
}

public interface ICustomMcuDeployOverlayGenerator
{
	ICustomMcuDeployOverlay CreateOverlay(Actor self, WorldRenderer wr, ActorInfo intoActor);
}

public interface ICustomMcuDeployOverlay
{
	IEnumerable<IRenderable> Render(Actor self, WorldRenderer wr, CPos topLeft, Dictionary<CPos, PlaceBuildingCellType> footprint);

	IEnumerable<IRenderable> RenderAnnotations(Actor self, WorldRenderer wr, CPos topLeft, Dictionary<CPos, PlaceBuildingCellType> footprint);
}

public interface ITransforms
{
	bool CanDeploy(Actor self);
}

public interface IOrderPreviewRender
{
	IEnumerable<IRenderable> Render(Actor self, WorldRenderer wr);

	IEnumerable<IRenderable> RenderAboveShroud(Actor self, WorldRenderer wr);

	IEnumerable<IRenderable> RenderAnnotations(Actor self, WorldRenderer wr);
}

public interface ITransformsPreview
{
	IEnumerable<IRenderable> Render(Actor self, WorldRenderer wr) { yield break; }

	IEnumerable<IRenderable> RenderAboveShroud(Actor self, WorldRenderer wr) { yield break; }

	IEnumerable<IRenderable> RenderAnnotations(Actor self, WorldRenderer wr) { yield break; }
}

public interface ICustomBuildingInfo
{
	bool CanPlaceBuilding(OpenRA.World world, CPos cell, Actor toIgnore);

	bool IsCellBuildable(OpenRA.World world, CPos cell, Actor? toIgnore = null);

	Dictionary<CPos, PlaceBuildingCellType> GetBuildingPlacementFootprint(OpenRA.World world, CPos cell, Actor toIgnore);

	IEnumerable<CPos> Tiles(CPos location);

	WPos GetCenterOfFootprint(CPos location);
}

public interface INotifyBuildingRepair
{
	void RepairStarted(Actor self);

	void RepairInterrupted(Actor self);
}

public interface INotifyWallBuilding
{
	void WallBuilding(Actor self, CPos location);

	void WallBuildingCompleted(Actor self, CPos location);

	void WallCreated(Actor self, Actor wall);

	void WallBuildingCanceled(Actor self, CPos location);
}

public interface ISubActor
{
	void OnParentKilled(Actor self, Actor parentActor);
}
