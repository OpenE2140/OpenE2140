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

using System.Diagnostics.CodeAnalysis;
using OpenRA.Activities;
using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Mods.OpenE2140.Orders;
using OpenRA.Mods.OpenE2140.Traits.Mcu;
using OpenRA.Primitives;
using OpenRA.Traits;
using static OpenRA.Mods.OpenE2140.Traits.Mcu.Mcu;
using OpenE2140Transform = OpenRA.Mods.OpenE2140.Activites.Transform;

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
	public readonly string[] TransformSounds = [];

	[Desc("Sounds to play when the transformation is blocked.")]
	public readonly string[] NoTransformSounds = [];

	[NotificationReference("Speech")]
	[Desc("Speech notification to play when transforming.")]
	public readonly string? TransformNotification;

	[FluentReference(optional: true)]
	[Desc("Text notification to display when transforming.")]
	public readonly string? TransformTextNotification;

	[NotificationReference("Speech")]
	[Desc("Speech notification to play when the transformation is blocked.")]
	public readonly string? NoTransformNotification;

	[FluentReference(optional: true)]
	[Desc("Text notification to display when the transformation is blocked.")]
	public readonly string? NoTransformTextNotification;

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

public class WaterBaseTransforms : PausableConditionalTrait<WaterBaseTransformsInfo>,
	IIssueOrder, IResolveOrder, IOrderVoice, IIssueDeployOrder, IOrderPreviewRender, INotifyTransform, ITransforms, IRenderAboveShroud, IRenderAnnotations
{
	private const string BeginPlaceDockOrderID = "BeginPlaceWaterBaseDock";
	private const string BuildWaterBaseOrderID = OrderConstants.BuildWaterBaseOrderID;

	private readonly Actor self;
	private readonly string faction;

	private McuDeployOverlay? mcuDeployOverlay;

	public ActorInfo ActorInfo { get; private set; }
	public CustomBuildingInfo CustomBuildingInfo { get; private set; }
	public ActorInfo DockActorInfo { get; private set; }
	public BuildingInfo DockBuildingInfo { get; private set; }
	public CPos? DockLocation { get; private set; }

	public WaterBaseTransforms(ActorInitializer init, WaterBaseTransformsInfo info)
		: base(info)
	{
		this.self = init.Self;
		this.ActorInfo = this.self.World.Map.Rules.Actors[info.IntoActor];
		this.DockActorInfo = this.self.World.Map.Rules.Actors[info.DockActor];
		this.CustomBuildingInfo = this.ActorInfo.TraitInfoOrDefault<CustomBuildingInfo>();
		this.DockBuildingInfo = this.DockActorInfo.TraitInfoOrDefault<BuildingInfo>();
		this.faction = init.GetValue<FactionInit, string>(this.self.Owner.Faction.InternalName);
	}

	protected override void Created(Actor self)
	{
		base.Created(self);

		this.mcuDeployOverlay = this.self.TraitOrDefault<McuDeployOverlay>();
	}

	public string? VoicePhraseForOrder(Actor self, Order order)
	{
		return order.OrderString == BuildWaterBaseOrderID ? this.Info.Voice : null;
	}

	public bool CanDeploy(Actor self, CPos? targetLocation = null)
	{
		if (this.IsTraitPaused || this.IsTraitDisabled)
			return false;

		targetLocation ??= self.Location;
		if (!self.World.Map.Contains(targetLocation.Value))
			return false;

		// First check, if the main building can be deployed at specified/current location.
		if (!this.CustomBuildingInfo.CanPlaceBuilding(self.World, targetLocation.Value + this.Info.Offset, BuildingActorCheck.Ignoring(self)))
			return false;

		// Now check, if there are any cells in buildable radius, where the dock can be placed.
		if (this.DockLocation == null)
			return this.GetPossibleCellsForDockPlacement(targetLocation).Any();
		else
			return this.GetPossibleCellsForDockPlacement(targetLocation).Contains(this.DockLocation.Value);
	}

	public bool CanPlaceDock(CPos location, CPos? deployLocation = null)
	{
		if (this.IsTraitPaused || this.IsTraitDisabled)
			return false;

		return this.GetPossibleCellsForDockPlacement(deployLocation).Contains(location);
	}

	public IEnumerable<CPos> GetPossibleCellsForDockPlacement(CPos? deployLocation = null)
	{
		return this.GetCellsInRangeForDock(deployLocation)
			.Where(c => !this.self.World.ShroudObscures(c) && this.self.World.CanPlaceBuilding(c, this.DockActorInfo, this.DockBuildingInfo, this.self));
	}

	//public IEnumerable<CPos> GetBuildableCellsForDock()
	//{
	//	return this.GetCellsInRangeForDock()
	//		.Where(c => !this.self.World.ShroudObscures(c) && this.self.World.IsCellBuildable(c, this.DockActorInfo, this.DockBuildingInfo, this.self));
	//}

	private IEnumerable<CPos> GetCellsInRangeForDock(CPos? deployLocation = null)
	{
		deployLocation ??= this.self.Location;

		var centerOfFootprint = this.GetCenterOfFootprint(deployLocation);

		return this.self.World.Map
			.FindTilesInAnnulus(deployLocation.Value, 0, (this.Info.MaximumDockDistance.Length / this.self.World.Map.Grid.TileScale) + 1)
			.Where(c => (this.self.World.Map.CenterOfCell(c) - centerOfFootprint).Length <= this.Info.MaximumDockDistance.Length);
	}

	public WPos GetCenterOfFootprint(CPos? deployLocation = null)
	{
		deployLocation ??= this.self.Location;
		return this.CustomBuildingInfo.GetCenterOfFootprint(deployLocation.Value + this.Info.Offset);
	}

	private IEnumerable<Order> ClearBlockersOrders(CPos topLeft)
	{
		return AIUtils.ClearBlockersOrders(this.CustomBuildingInfo.Tiles(topLeft).ToList(), this.self.Owner, this.self);
	}

	public IEnumerable<IOrderTargeter> Orders
	{
		get
		{
			if (!this.IsTraitDisabled)
				yield return new BeginPlaceDockOrderTargeter(this);
		}
	}

	Order? IIssueOrder.IssueOrder(Actor self, IOrderTargeter order, in Target target, bool queued)
	{
		if (order.OrderID == BeginPlaceDockOrderID)
			return this.BeginPlaceDock(self, target, queued);
		else if (order.OrderID == BuildWaterBaseOrderID)
			return new Order(BuildWaterBaseOrderID, self, target, queued);

		return null;
	}

	private Order BeginPlaceDock(Actor self, in Target target, bool queued)
	{
		var targetLocation = self.World.Map.CellContaining(target.CenterPosition);
		if (this.CanDeploy(self, targetLocation))
			self.World.OrderGenerator = new PlaceDockOrderGenerator(self, targetLocation, queued);

		return new Order(BeginPlaceDockOrderID, self, Target.FromCell(self.World, targetLocation), queued);
	}

	Order? IIssueDeployOrder.IssueDeployOrder(Actor self, bool queued)
	{
		return this.BeginPlaceDock(self, Target.FromCell(self.World, self.Location), queued);
	}

	bool IIssueDeployOrder.CanIssueDeployOrder(Actor self, bool queued)
	{
		return !this.IsTraitPaused && !this.IsTraitDisabled && this.CanDeploy(this.self);
	}

	void IResolveOrder.ResolveOrder(Actor self, Order order)
	{
		if (this.IsTraitPaused || this.IsTraitDisabled)
			return;

		if (order.OrderString == "Stop")
		{
			this.DockLocation = null;
			self.CancelActivity();
			return;
		}

		if (order.Target.Type != TargetType.Terrain)
			return;

		if (order.OrderString == BuildWaterBaseOrderID)
		{
			var cell = self.World.Map.CellContaining(order.Target.CenterPosition);
			var deployLocation = order.ExtraLocation;

			if (!this.ValidateDeployTransform(deployLocation, cell, order.Queued))
				return;

			this.DockLocation = cell;

			this.self.QueueActivity(order.Queued, this.GetTransformActivity(deployLocation, cell));
			self.ShowTargetLines();
		}
	}

	private bool ValidateDeployTransform(CPos deployLocation, CPos dockLocation, bool queued)
	{
		if (!queued && (!this.CanDeploy(this.self, deployLocation) || !this.CanPlaceDock(dockLocation, deployLocation)))
		{
			foreach (var order in this.ClearBlockersOrders(this.self.Location + this.Info.Offset))
				this.self.World.IssueOrder(order);

			// Only play the "Cannot deploy here" audio
			// for non-queued orders
			foreach (var s in this.Info.NoTransformSounds)
				Game.Sound.PlayToPlayer(SoundType.World, this.self.Owner, s);

			Game.Sound.PlayNotification(
				this.self.World.Map.Rules, this.self.Owner, "Speech", this.Info.NoTransformNotification, this.self.Owner.Faction.InternalName);
			TextNotificationsManager.AddTransientLine(this.self.Owner, this.Info.NoTransformTextNotification);

			return false;
		}

		return true;
	}

	private Activity GetTransformActivity(CPos deployLocation, CPos dockLocation)
	{
		if (deployLocation == this.self.Location)
		{
			return new OpenE2140Transform(this.Info.IntoActor)
			{
				Offset = this.Info.Offset,
				Facing = this.Info.Facing,
				Sounds = this.Info.TransformSounds,
				Notification = this.Info.TransformNotification,
				TextNotification = this.Info.TransformTextNotification,
				Faction = this.faction
			};
		}

		return new MoveToTransform(this.self, deployLocation, dockLocation, this);
	}

	IEnumerable<IRenderable> IOrderPreviewRender.Render(Actor self, WorldRenderer wr, Target target)
	{
		return RenderOrderPreviewOverlay(self, wr, target, Game.GetModifierKeys(), (p, t) => p.Render(self, wr, t));
	}

	IEnumerable<IRenderable> IOrderPreviewRender.RenderAboveShroud(Actor self, WorldRenderer wr, Target target)
	{
		return RenderOrderPreviewOverlay(self, wr, target, Game.GetModifierKeys(), (p, t) => p.RenderAboveShroud(self, wr, t));
	}

	IEnumerable<IRenderable> IOrderPreviewRender.RenderAnnotations(Actor self, WorldRenderer wr, Target target)
	{
		return RenderOrderPreviewOverlay(self, wr, target, Game.GetModifierKeys(), (p, t) => p.RenderAnnotations(self, wr, t));
	}

	private static IEnumerable<IRenderable> RenderOrderPreviewOverlay(
		Actor self, WorldRenderer wr, Target target, Modifiers modifiers,
		Func<ITransformsPreview, Target, IEnumerable<IRenderable>> renderFunc)
	{
		if (modifiers.HasModifier(Modifiers.Ctrl))
		{
			var cell = wr.Viewport.ViewToWorld(Viewport.LastMousePos);
			if (!self.World.Map.Contains(cell))
				yield break;
			target = Target.FromCell(self.World, cell);
		}

		var previewTraits = self.TraitsImplementing<ITransformsPreview>();
		foreach (var item in previewTraits)
			foreach (var r in renderFunc(item, target))
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

	IEnumerable<IRenderable> IRenderAboveShroud.RenderAboveShroud(Actor self, WorldRenderer wr)
	{
		if (this.mcuDeployOverlay == null || !TryGetActiveDockPlacement(self, out var generator))
			return [];

		var target = generator.DeployLocation != null
			? Target.FromCell(self.World, generator.DeployLocation.Value)
			: Target.FromActor(self);

		return this.mcuDeployOverlay.RenderAboveShroud(self, wr, target);
	}

	bool IRenderAboveShroud.SpatiallyPartitionable => false;

	IEnumerable<IRenderable> IRenderAnnotations.RenderAnnotations(Actor self, WorldRenderer wr)
	{
		if (this.mcuDeployOverlay == null || !TryGetActiveDockPlacement(self, out var generator))
			return [];

		var target = generator.DeployLocation != null
			? Target.FromCell(self.World, generator.DeployLocation.Value)
			: Target.FromActor(self);

		return this.mcuDeployOverlay.RenderAnnotations(self, wr, target);
	}

	bool IRenderAnnotations.SpatiallyPartitionable => false;

	private static bool TryGetActiveDockPlacement(Actor self, [NotNullWhen(true)] out PlaceDockOrderGenerator? generator)
	{
		if (self.World.OrderGenerator is PlaceDockOrderGenerator o && o.Self == self)
		{
			generator = o;
			return true;
		}

		generator = null;
		return false;
	}

	private class PlaceDockOrderGenerator : ExtendedUnitOrderGenerator
	{
		public readonly Actor Self;
		private readonly bool queued;
		private readonly WaterBaseTransforms transforms;

		private CPos? deployLocation;

		public CPos? DeployLocation => this.deployLocation;

		protected override MouseActionType ActionType => MouseActionType.PlaceBuilding;

		public PlaceDockOrderGenerator(Actor self, CPos? deployLocation, bool queued)
			: base(self.World)
		{
			this.Self = self;
			this.deployLocation = deployLocation;
			this.queued = queued;
			this.transforms = self.Trait<WaterBaseTransforms>();
		}

		protected override IEnumerable<Order> OrderInner(OpenRA.World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			if (mi.Button != this.ActionButton)
			{
				world.CancelInputMode();
				yield break;
			}

			if (!world.Map.Contains(cell))
			{
				world.CancelInputMode();
				yield break;
			}

			if (this.deployLocation == null && mi.Modifiers.HasModifier(Modifiers.Ctrl))
			{
				this.deployLocation = cell;
				yield break;
			}

			if (this.deployLocation != null && this.transforms.CanPlaceDock(cell, this.deployLocation))
			{
				yield return new Order(BuildWaterBaseOrderID, this.Self, Target.FromCell(world, cell), this.queued)
				{
					ExtraLocation = this.deployLocation.Value
				};
			}

			world.CancelInputMode();
		}

		public override void SelectionChanged(OpenRA.World world, IEnumerable<Actor> selected)
		{
			world.CancelInputMode();
		}

		public override IEnumerable<IRenderable> Render(WorldRenderer wr, OpenRA.World world) { yield break; }
		public override IEnumerable<IRenderable> RenderAboveShroud(WorldRenderer wr, OpenRA.World world)
		{
			var cell = wr.Viewport.ViewToWorld(Viewport.LastMousePos);
			if (!world.Map.Contains(cell))
				yield break;

			var footprint = new Dictionary<CPos, PlaceBuildingCellType>();

			foreach (var t in this.transforms.DockBuildingInfo.Tiles(cell))
			{
				footprint.Add(t, this.transforms.CanPlaceDock(cell, deployLocation: this.deployLocation) ? PlaceBuildingCellType.Valid : PlaceBuildingCellType.Invalid);
			}

			foreach (var r in this.RenderPlaceBuildingPreviews(this.Self, wr, cell, footprint))
				yield return r;
		}

		// TODO: maybe refactor with McuDeployOverlay.RenderPlaceBuildingPreviews ?
		private IEnumerable<IRenderable> RenderPlaceBuildingPreviews(Actor self, WorldRenderer wr, CPos topLeft, Dictionary<CPos, PlaceBuildingCellType> footprint)
		{
			var previewGeneratorInfos = this.transforms.DockActorInfo.TraitInfos<IPlaceBuildingPreviewGeneratorInfo>();
			if (previewGeneratorInfos.Count > 0)
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

		public override IEnumerable<IRenderable> RenderAnnotations(WorldRenderer wr, OpenRA.World world) { yield break; }

		public override string GetCursor(OpenRA.World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			if (this.deployLocation == null)
				return this.transforms.CanDeploy(this.Self, cell) ? this.transforms.Info.DeployCursor : this.transforms.Info.DeployBlockedCursor;
			else
				return this.transforms.CanPlaceDock(cell, this.deployLocation.Value)
					? this.transforms.Info.DeployCursor
					: this.transforms.Info.DeployBlockedCursor;
		}
	}

	private class BeginPlaceDockOrderTargeter : IOrderTargeter
	{
		private readonly WaterBaseTransforms transforms;

		public string OrderID => BeginPlaceDockOrderID;
		public int OrderPriority => 5;

		public BeginPlaceDockOrderTargeter(WaterBaseTransforms transforms)
		{
			this.transforms = transforms;
		}

		public bool TargetOverridesSelection(Actor self, in Target target, List<Actor> actorsAt, CPos xy, TargetModifiers modifiers)
		{
			return true;
		}

		public bool CanTarget(Actor self, in Target target, ref TargetModifiers modifiers, ref string cursor)
		{
			if (target.Type == TargetType.Invalid)
				return false;
			else if (target.Type == TargetType.Terrain && !modifiers.HasModifier(TargetModifiers.ForceAttack))
				return false;

			var location = self.World.Map.CellContaining(target.CenterPosition);
			if (!self.World.Map.Contains(location))
				return false;

			cursor = this.transforms.CanDeploy(self, location) ? this.transforms.Info.DeployCursor : this.transforms.Info.DeployBlockedCursor;

			this.IsQueued = modifiers.HasModifier(TargetModifiers.ForceQueue);

			if (target.Type == TargetType.Actor)
				return self == target.Actor;

			return true;
		}

		public bool IsQueued { get; private set; }
	}

	private class BeginPlaceDockOrderGenerator : ExtendedUnitOrderGenerator
	{
		private readonly Actor self;
		private readonly WaterBaseTransforms transforms;

		public BeginPlaceDockOrderGenerator(Actor self, WaterBaseTransforms transforms)
			: base(self.World)
		{
			this.self = self;
			this.transforms = transforms;
		}

		protected override IEnumerable<Order> OrderInner(OpenRA.World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			if (mi.Modifiers.HasModifier(Modifiers.Ctrl))
			{
				var queued = mi.Modifiers.HasModifier(Modifiers.Shift);
				world.OrderGenerator = new PlaceDockOrderGenerator(this.self, cell, queued);
			}

			return [];
		}
		public override void SelectionChanged(OpenRA.World world, IEnumerable<Actor> selected)
		{
			world.CancelInputMode();
		}

		public override string GetCursor(OpenRA.World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			if (!world.Map.Contains(cell))
				return this.transforms.Info.DeployBlockedCursor;

			if (mi.Modifiers.HasModifier(Modifiers.Ctrl) || this.transforms.CanDeploy(this.self, cell))
				return this.transforms.Info.DeployCursor;

			return this.transforms.Info.DeployBlockedCursor;
		}
	}


	private class MoveToTransform : Activity
	{
		private readonly CPos targetLocation;
		private readonly CPos dockLocation;
		private readonly WaterBaseTransforms transforms;
		private readonly IMove? move;
		private readonly IMoveInfo? moveInfo;

		private int attempt;

		public MoveToTransform(Actor self, CPos targetLocation, CPos dockLocation, WaterBaseTransforms transforms)
		{
			this.targetLocation = targetLocation;
			this.dockLocation = dockLocation;
			this.transforms = transforms;

			this.move = self.TraitOrDefault<IMove>();
			this.moveInfo = self.Info.TraitInfo<IMoveInfo>();
		}

		public override bool Tick(Actor self)
		{
			if (this.IsCanceling || self.Disposed)
				return true;

			if (self.Location != this.targetLocation)
			{
				if (this.move == null)
					return true;

				// Limit number of move attempts
				if (++this.attempt > 3)
				{
					return true;
				}

				if (this.attempt > 1)
					this.QueueChild(new Wait(30));

				var moveActivity = this.move.MoveTo(this.targetLocation, targetLineColor: this.moveInfo?.GetTargetLineColor());
				this.QueueChild(moveActivity);
				return false;
			}

			if (this.transforms.ValidateDeployTransform(this.targetLocation, this.dockLocation, false))
				this.QueueChild(this.transforms.GetTransformActivity(this.targetLocation, this.dockLocation));

			return true;
		}

		public override IEnumerable<TargetLineNode> TargetLineNodes(Actor self)
		{
			if (this.ChildActivity != null)
				return this.ChildActivity.TargetLineNodes(self);

			if (this.moveInfo != null)
				return [new TargetLineNode(Target.FromCell(self.World, this.targetLocation), this.moveInfo.GetTargetLineColor())];

			return [];
		}
	}
}
