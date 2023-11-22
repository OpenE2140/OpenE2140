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

using OpenRA.Activities;
using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Activites;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Primitives;
using OpenRA.Traits;
using static OpenRA.Mods.OpenE2140.Traits.Mcu.Mcu;

namespace OpenRA.Mods.OpenE2140.Traits.WaterBase;

public class WaterBaseTransformsInfo : PausableConditionalTraitInfo, ITransformsInfo
{
	[ActorReference]
	[FieldLoader.Require]
	[Desc("Actor to transform into.")]
	public readonly string IntoActor = null!;

	[Desc("Offset to spawn the transformed actor relative to the current cell.")]
	public readonly CVec Offset = CVec.Zero;

	[Desc("Facing that the actor must face before transforming.")]
	public readonly WAngle Facing = new(384);

	[Desc("Sounds to play when transforming.")]
	public readonly string[] TransformSounds = Array.Empty<string>();

	[Desc("Sounds to play when the transformation is blocked.")]
	public readonly string[] NoTransformSounds = Array.Empty<string>();

	[NotificationReference("Speech")]
	[Desc("Speech notification to play when transforming.")]
	public readonly string? TransformNotification = null;

	[TranslationReference(optional: true)]
	[Desc("Text notification to display when transforming.")]
	public readonly string? TransformTextNotification = null;

	[NotificationReference("Speech")]
	[Desc("Speech notification to play when the transformation is blocked.")]
	public readonly string? NoTransformNotification = null;

	[TranslationReference(optional: true)]
	[Desc("Text notification to display when the transformation is blocked.")]
	public readonly string? NoTransformTextNotification = null;

	[CursorReference]
	[Desc("Cursor to display when able to (un)deploy the actor.")]
	public readonly string DeployCursor = "deploy";

	[CursorReference]
	[Desc("Cursor to display when unable to (un)deploy the actor.")]
	public readonly string DeployBlockedCursor = "deploy-blocked";

	[VoiceReference]
	public readonly string Voice = "Action";

	[Desc("Maximum distance from MCU deployment position, where a dock can be placed.")]
	public readonly WDist MaximumDockDistance = new WDist(3712);

	[ActorReference]
	[Desc("Name of the dock actor.")]
	[FieldLoader.Require]
	public readonly string DockActor = null!;

	string? ITransformsInfo.IntoActor => this.IntoActor;

	CVec ITransformsInfo.Offset => this.Offset;

	public override object Create(ActorInitializer init)
	{
		return new WaterBaseTransforms(init, this);
	}
}

public class WaterBaseTransforms : PausableConditionalTrait<WaterBaseTransformsInfo>, IIssueOrder, IResolveOrder, IOrderVoice, IIssueDeployOrder, IOrderPreviewRender, INotifyTransform, ITransforms
{
	private const string BeginPlaceDockOrderID = "BeginPlaceWaterBaseDock";
	private const string BuildWaterBaseOrderID = "BuildWaterBase";

	private readonly Actor self;
	private readonly string faction;

	public ActorInfo ActorInfo { get; private set; }
	public BuildingInfo BuildingInfo { get; private set; }
	public ActorInfo DockActorInfo { get; private set; }
	public BuildingInfo DockBuildingInfo { get; private set; }
	public CPos? DockLocation { get; private set; }

	public WaterBaseTransforms(ActorInitializer init, WaterBaseTransformsInfo info)
		: base(info)
	{
		this.self = init.Self;
		this.ActorInfo = this.self.World.Map.Rules.Actors[info.IntoActor];
		this.DockActorInfo = this.self.World.Map.Rules.Actors[info.DockActor];
		this.BuildingInfo = this.ActorInfo.TraitInfoOrDefault<BuildingInfo>();
		this.DockBuildingInfo = this.DockActorInfo.TraitInfoOrDefault<BuildingInfo>();
		this.faction = init.GetValue<FactionInit, string>(this.self.Owner.Faction.InternalName);
	}

	public string? VoicePhraseForOrder(Actor self, Order order)
	{
		return order.OrderString == BuildWaterBaseOrderID ? this.Info.Voice : null;
	}

	public bool CanDeploy(Actor self)
	{
		if (this.IsTraitPaused || this.IsTraitDisabled)
			return false;

		// First check, if the main building can be deployed at current location.
		var footprintCells = this.BuildingInfo.Tiles(self.Location + this.Info.Offset).ToList();
		if (footprintCells.Any(c => !this.self.World.IsCellBuildable(c, this.ActorInfo, this.BuildingInfo, this.self)))
			return false;

		// Now check, if there are any cells in buildable radius, where the dock can be placed.
		if (this.DockLocation == null)
			return this.GetPossibleCellsForDockPlacement().Any();
		else
			return this.GetPossibleCellsForDockPlacement().Contains(this.DockLocation.Value);
	}

	public bool CanPlaceDock(CPos location)
	{
		if (this.IsTraitPaused || this.IsTraitDisabled)
			return false;

		return this.GetPossibleCellsForDockPlacement().Contains(location);
	}

	public IEnumerable<CPos> GetPossibleCellsForDockPlacement()
	{
		return this.GetCellsInRangeForDock()
			.Where(c => !this.self.World.ShroudObscures(c) && this.self.World.CanPlaceBuilding(c, this.DockActorInfo, this.DockBuildingInfo, this.self));
	}

	public IEnumerable<CPos> GetBuildableCellsForDock()
	{
		return this.GetCellsInRangeForDock()
			.Where(c => !this.self.World.ShroudObscures(c) && this.self.World.IsCellBuildable(c, this.DockActorInfo, this.DockBuildingInfo, this.self));
	}

	private IEnumerable<CPos> GetCellsInRangeForDock()
	{
		var centerOfFootprint = this.GetCenterOfFootprint();

		return this.self.World.Map.FindTilesInAnnulus(this.self.Location, 0, (this.Info.MaximumDockDistance.Length / this.self.World.Map.Grid.TileScale) + 1)
			.Where(c => (this.self.World.Map.CenterOfCell(c) - centerOfFootprint).Length <= this.Info.MaximumDockDistance.Length);
	}

	public WPos GetCenterOfFootprint()
	{
		var footprint = this.BuildingInfo.Tiles(this.self.Location + this.Info.Offset);
		var (topLeft, bottomRight) = GetBounds(footprint);

		return topLeft + (bottomRight - topLeft) / 2;
	}

	private static (WPos topLeft, WPos bottomRight) GetBounds(IEnumerable<CPos> cells)
	{
		var left = int.MaxValue;
		var right = int.MinValue;
		var top = int.MaxValue;
		var bottom = int.MinValue;

		foreach (var cell in cells)
		{
			left = Math.Min(left, cell.X);
			right = Math.Max(right, cell.X);
			top = Math.Min(top, cell.Y);
			bottom = Math.Max(bottom, cell.Y);
		}

		return (new WPos(1024 * left, 1024 * top, 0),
			new WPos(1024 * right + 1024, 1024 * bottom + 1024, 0));
	}

	private IEnumerable<Order> ClearBlockersOrders(CPos topLeft)
	{
		return AIUtils.ClearBlockersOrders(this.BuildingInfo.Tiles(topLeft).ToList(), this.self.Owner, this.self);
	}

	public Activity GetTransformActivity()
	{
		return new Transform(this.Info.IntoActor)
		{
			Offset = this.Info.Offset,
			Facing = this.Info.Facing,
			Sounds = this.Info.TransformSounds,
			Notification = this.Info.TransformNotification,
			TextNotification = this.Info.TransformTextNotification,
			Faction = faction
		};
	}

	public IEnumerable<IOrderTargeter> Orders
	{
		get
		{
			if (!this.IsTraitDisabled)
				yield return new DeployOrderTargeter(BeginPlaceDockOrderID, 5,
					() => this.CanDeploy(this.self) ? this.Info.DeployCursor : this.Info.DeployBlockedCursor);
		}
	}

	public Order? IssueOrder(Actor self, IOrderTargeter order, in Target target, bool queued)
	{
		if (order.OrderID == BeginPlaceDockOrderID)
			return this.BeginPlaceDock(queued);
		else if (order.OrderID == BuildWaterBaseOrderID)
			return new Order(BuildWaterBaseOrderID, self, target, queued);

		return null;
	}

	public void ResolveOrder(Actor self, Order order)
	{
		if (this.IsTraitPaused || this.IsTraitDisabled)
			return;

		CPos? cellLocation = order.Target.Type == TargetType.Terrain ? self.World.Map.CellContaining(order.Target.CenterPosition) : null;
		if (order.OrderString == BuildWaterBaseOrderID && this.CanDeploy(self) && cellLocation != null && this.CanPlaceDock(cellLocation.Value))
		{
			Sync.RunUnsynced(self.World, () => this.self.World.CancelInputMode());
			this.DockLocation = cellLocation;
			this.DeployTransform(order.Queued);
		}
		else if (order.OrderString == BeginPlaceDockOrderID && this.CanDeploy(self))
		{
			Sync.RunUnsynced(self.World, () => this.self.World.OrderGenerator = new PlaceDockOrderGenerator(this.self, order.Queued));
		}
	}

	private Order BeginPlaceDock(bool queued)
	{
		return new Order(BeginPlaceDockOrderID, this.self, queued);
	}

	Order IIssueDeployOrder.IssueDeployOrder(Actor self, bool queued)
	{
		return this.BeginPlaceDock(queued);
	}

	bool IIssueDeployOrder.CanIssueDeployOrder(Actor self, bool queued) { return !this.IsTraitPaused && !this.IsTraitDisabled && this.CanDeploy(this.self); }

	public void DeployTransform(bool queued)
	{
		if (!queued && (!this.CanDeploy(this.self) || this.DockLocation == null || !this.CanPlaceDock(this.DockLocation.Value)))
		{
			foreach (var order in this.ClearBlockersOrders(this.self.Location + this.Info.Offset))
				this.self.World.IssueOrder(order);

			// Only play the "Cannot deploy here" audio
			// for non-queued orders
			foreach (var s in this.Info.NoTransformSounds)
				Game.Sound.PlayToPlayer(SoundType.World, this.self.Owner, s);

			Game.Sound.PlayNotification(this.self.World.Map.Rules, this.self.Owner, "Speech", this.Info.NoTransformNotification, this.self.Owner.Faction.InternalName);
			TextNotificationsManager.AddTransientLine(this.self.Owner, this.Info.NoTransformTextNotification);

			return;
		}

		this.self.QueueActivity(queued, this.GetTransformActivity());
	}

	IEnumerable<IRenderable> IOrderPreviewRender.Render(Actor self, WorldRenderer wr)
	{
		var previewTraits = self.TraitsImplementing<ITransformsPreview>();
		foreach (var item in previewTraits)
			foreach (var r in item.Render(self, wr))
				yield return r;
	}

	IEnumerable<IRenderable> IOrderPreviewRender.RenderAboveShroud(Actor self, WorldRenderer wr)
	{
		var previewTraits = self.TraitsImplementing<ITransformsPreview>();
		foreach (var item in previewTraits)
			foreach (var r in item.RenderAboveShroud(self, wr))
				yield return r;
	}

	IEnumerable<IRenderable> IOrderPreviewRender.RenderAnnotations(Actor self, WorldRenderer wr)
	{
		var previewTraits = self.TraitsImplementing<ITransformsPreview>();
		foreach (var item in previewTraits)
			foreach (var r in item.RenderAnnotations(self, wr))
				yield return r;
	}

	void INotifyTransform.TransformCanceled(Actor self)
	{
		this.DockLocation = null;
	}

	void INotifyTransform.BeforeTransform(Actor self)
	{
		// noop
	}

	void INotifyTransform.OnTransform(Actor self)
	{
		// noop
	}

	void INotifyTransform.AfterTransform(Actor toActor)
	{
		if (this.DockLocation == null)
			return;

		var init = new TypeDictionary
		{
			new LocationInit(this.DockLocation.Value),
			new OwnerInit(this.self.Owner),
			new HealthInit(toActor.Trait<IHealth>().GetHPPercentage()),
			new WaterBaseDockInit(toActor),
			new McuInit() // hack to enable TransformSequence
		};
		if (this.faction != null)
		{
			init.Add(new FactionInit(this.faction));
		}

		var dockActor = this.self.World.CreateActor(this.DockActorInfo.Name, init);

		if (this.self.World.Selection.Contains(toActor))
			this.self.World.Selection.Add(dockActor);

		var controlGroup = this.self.World.ControlGroups.GetControlGroupForActor(toActor);
		if (controlGroup.HasValue)
			this.self.World.ControlGroups.AddToControlGroup(dockActor, controlGroup.Value);
	}

	private class PlaceDockOrderGenerator : OrderGenerator
	{
		private readonly bool queued;
		private readonly Actor self;
		private readonly WaterBaseTransforms transforms;

		public PlaceDockOrderGenerator(Actor self, bool queued)
		{
			this.queued = queued;
			this.self = self;
			this.transforms = self.Trait<WaterBaseTransforms>();
		}

		protected override IEnumerable<Order> OrderInner(OpenRA.World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			if (mi.Button == Game.Settings.Game.MouseButtonPreference.Cancel)
			{
				world.CancelInputMode();
				yield break;
			}

			if (mi.Button == Game.Settings.Game.MouseButtonPreference.Action && this.transforms.CanPlaceDock(cell))
			{
				yield return new Order(BuildWaterBaseOrderID, this.self, Target.FromCell(world, cell), this.queued);
			}
		}

		protected override void SelectionChanged(OpenRA.World world, IEnumerable<Actor> selected)
		{
			world.CancelInputMode();
		}

		protected override IEnumerable<IRenderable> Render(WorldRenderer wr, OpenRA.World world) { yield break; }
		protected override IEnumerable<IRenderable> RenderAboveShroud(WorldRenderer wr, OpenRA.World world)
		{
			var lastMousePos = wr.Viewport.ViewToWorld(Viewport.LastMousePos);

			var footprint = new Dictionary<CPos, PlaceBuildingCellType>();

			foreach (var t in this.transforms.DockBuildingInfo.Tiles(lastMousePos))
			{
				footprint.Add(t, this.transforms.CanPlaceDock(lastMousePos) ? PlaceBuildingCellType.Valid : PlaceBuildingCellType.Invalid);
			}

			foreach (var r in this.RenderPlaceBuildingPreviews(this.self, wr, lastMousePos, footprint))
				yield return r;
		}

		// TODO: maybe refactor with McuDeployOverlay ?
		private IEnumerable<IRenderable> RenderPlaceBuildingPreviews(Actor self, WorldRenderer wr, CPos topLeft, Dictionary<CPos, PlaceBuildingCellType> footprint)
		{
			var previewGeneratorInfos = this.transforms.DockActorInfo.TraitInfos<IPlaceBuildingPreviewGeneratorInfo>();
			if (previewGeneratorInfos.Any())
			{
				var td = new TypeDictionary()
				{
					new FactionInit(self.Owner.Faction.InternalName),
					new OwnerInit(self.Owner),
				};

				foreach (var api in this.transforms.DockActorInfo.TraitInfos<IActorPreviewInitInfo>())
					foreach (var o in api.ActorPreviewInits(this.transforms.DockActorInfo, ActorPreviewType.PlaceBuilding))
						td.Add(o);

				foreach (var gen in previewGeneratorInfos)
				{
					var preview = gen.CreatePreview(wr, this.transforms.DockActorInfo, td);
					foreach (var r in preview.Render(wr, topLeft, footprint))
						yield return r;
				}
			}
		}

		protected override IEnumerable<IRenderable> RenderAnnotations(WorldRenderer wr, OpenRA.World world) { yield break; }

		protected override string GetCursor(OpenRA.World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			return this.transforms.CanPlaceDock(cell) ? this.transforms.Info.DeployCursor : this.transforms.Info.DeployBlockedCursor;
		}
	}
}
