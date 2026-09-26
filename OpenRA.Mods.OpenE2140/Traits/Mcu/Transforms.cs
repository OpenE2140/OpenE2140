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
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Orders;
using OpenRA.Traits;
using Transform = OpenRA.Mods.OpenE2140.Activites.Transform;

namespace OpenRA.Mods.OpenE2140.Traits.Mcu;

[Desc("Actor becomes a specified actor type when this trait is triggered.",
	$"Special version of the default {nameof(Transforms)} trait, which provides additional features compared to the original trait.")]
public class TransformsInfo : PausableConditionalTraitInfo, ITransformsInfo
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

	string? ITransformsInfo.IntoActor => this.IntoActor;

	CVec ITransformsInfo.Offset => this.Offset;

	public override object Create(ActorInitializer init) { return new Transforms(init, this); }
}

public class Transforms : PausableConditionalTrait<TransformsInfo>, IIssueOrder, IResolveOrder, IOrderVoice, IIssueDeployOrder, ITransforms, IOrderPreviewRender
{
	private const string BeginDeployTransformOrderID = "BeginDeployTransformBuilding";
	private const string DeployTransformOrderID = "DeployTransform";

	private readonly Actor self;
	private readonly ActorInfo actorInfo;
	private readonly ICustomBuildingInfo? customBuildingInfo;
	private readonly string faction;

	public Transforms(ActorInitializer init, TransformsInfo info)
		: base(info)
	{
		this.self = init.Self;
		this.actorInfo = this.self.World.Map.Rules.Actors[info.IntoActor];
		this.customBuildingInfo = CustomBuildingInfoWrapper.WrapIfNecessary(this.actorInfo);
		this.faction = init.GetValue<FactionInit, string>(this.self.Owner.Faction.InternalName);
	}

	public string? VoicePhraseForOrder(Actor self, Order order)
	{
		return order.OrderString is DeployTransformOrderID or OrderConstants.MoveAndDeployTransformOrderID ? this.Info.Voice : null;
	}

	public bool CanDeploy(Actor self, CPos? targetLocation = null)
	{
		if (this.IsTraitPaused || this.IsTraitDisabled)
			return false;

		return this.customBuildingInfo?.CanPlaceBuilding(self.World, (targetLocation ?? self.Location) + this.Info.Offset, self) != false;
	}

	private IEnumerable<Order> ClearBlockersOrders(CPos topLeft)
	{
		return this.customBuildingInfo == null
			? []
			: AIUtils.ClearBlockersOrders(this.customBuildingInfo.Tiles(topLeft).ToList(), this.self.Owner, this.self);
	}

	public IEnumerable<IOrderTargeter> Orders
	{
		get
		{
			if (!this.IsTraitDisabled)
			{
				//yield return new DeployOrderTargeter(DeployTransformOrderID, 5,
				//	() => this.CanDeploy(this.self) ? this.Info.DeployCursor : this.Info.DeployBlockedCursor);
				yield return new BeginDeployTransformOrderTargeter(this, this.Info.DeployCursor, this.Info.DeployBlockedCursor);
			}
		}
	}

	public Order? IssueOrder(Actor self, IOrderTargeter order, in Target target, bool queued)
	{
		if (order.OrderID is DeployTransformOrderID or OrderConstants.MoveAndDeployTransformOrderID)
		{
			return new Order(order.OrderID, self, target, queued);
		}
		else if (order.OrderID == BeginDeployTransformOrderID)
			return BeginDeployTransform(self, queued);

		return null;
	}

	private static Order BeginDeployTransform(Actor self, bool queued)
	{
		self.World.OrderGenerator = new MoveAndTransformOrderGenerator(self, queued);
		return new Order(BeginDeployTransformOrderID, self, queued);
	}

	Order IIssueDeployOrder.IssueDeployOrder(Actor self, bool queued)
	{
		//return BeginDeployTransform(self, queued);
		return new Order(DeployTransformOrderID, self, queued);
	}

	bool IIssueDeployOrder.CanIssueDeployOrder(Actor self, bool queued)
	{
		return !this.IsTraitPaused && !this.IsTraitDisabled;
	}

	void IResolveOrder.ResolveOrder(Actor self, Order order)
	{
		if (this.IsTraitPaused || this.IsTraitDisabled)
			return;

		if (order.OrderString == DeployTransformOrderID)
			this.self.QueueActivity(order.Queued, this.GetTransformActivity(self.Location));
		else if (order.OrderString == OrderConstants.MoveAndDeployTransformOrderID)
		{
			// Only terrain target is supported.
			if (order.Target.Type != TargetType.Terrain)
			{
				return;
			}

			var deployLocation = self.World.Map.CellContaining(order.Target.CenterPosition);

			if (!this.ValidateDeployTransform(deployLocation, order.Queued))
				return;

			self.QueueActivity(order.Queued, new MoveToTransform(self, deployLocation, this));
			self.ShowTargetLines();
		}
	}
	private bool ValidateDeployTransform(CPos targetLocation, bool queued)
	{
		if (!queued && !this.CanDeploy(this.self, targetLocation))
		{
			foreach (var order in this.ClearBlockersOrders(targetLocation + this.Info.Offset))
				this.self.World.IssueOrder(order);

			// Only play the "Cannot deploy here" audio
			// for non-queued orders
			foreach (var s in this.Info.NoTransformSounds)
				Game.Sound.PlayToPlayer(SoundType.World, this.self.Owner, s);

			Game.Sound.PlayNotification(this.self.World.Map.Rules, this.self.Owner, "Speech", this.Info.NoTransformNotification, this.self.Owner.Faction.InternalName);
			TextNotificationsManager.AddTransientLine(this.self.Owner, this.Info.NoTransformTextNotification);

			return false;
		}

		return true;
	}

	private Activity GetTransformActivity(CPos deployLocation)
	{
		if (deployLocation == this.self.Location)
		{
			return new Transform(this.Info.IntoActor)
			{
				Offset = this.Info.Offset,
				Facing = this.Info.Facing,
				Sounds = this.Info.TransformSounds,
				Notification = this.Info.TransformNotification,
				TextNotification = this.Info.TransformTextNotification,
				Faction = this.faction
			};
		}

		return new MoveToTransform(this.self, deployLocation, this);
	}

	IEnumerable<IRenderable> IOrderPreviewRender.Render(Actor self, WorldRenderer wr, Target target)
	{
		return RenderOrderPreviewOverlay(self, p => p.Render(self, wr, target));
	}

	IEnumerable<IRenderable> IOrderPreviewRender.RenderAboveShroud(Actor self, WorldRenderer wr, Target target)
	{
		return RenderOrderPreviewOverlay(self, p => p.RenderAboveShroud(self, wr, target));
	}

	IEnumerable<IRenderable> IOrderPreviewRender.RenderAnnotations(Actor self, WorldRenderer wr, Target target)
	{
		return RenderOrderPreviewOverlay(self, p => p.RenderAnnotations(self, wr, target));
	}

	private static IEnumerable<IRenderable> RenderOrderPreviewOverlay(Actor self, Func<ITransformsPreview, IEnumerable<IRenderable>> renderFunc)
	{
		var previewTraits = self.TraitsImplementing<ITransformsPreview>();
		foreach (var item in previewTraits)
			foreach (var r in renderFunc(item))
				yield return r;
	}

	private class BeginDeployTransformOrderTargeter : IOrderTargeter
	{
		private readonly ITransforms transforms;
		private readonly string deployCursor;
		private readonly string deployBlockedCursor;

		public string OrderID { get; private set; } = OrderConstants.MoveAndDeployTransformOrderID;
		public int OrderPriority => 5;

		public BeginDeployTransformOrderTargeter(ITransforms transforms, string deployCursor, string deployBlockedCursor)
		{
			this.transforms = transforms;
			this.deployCursor = deployCursor;
			this.deployBlockedCursor = deployBlockedCursor;
		}

		public bool TargetOverridesSelection(Actor self, in Target target, List<Actor> actorsAt, CPos xy, TargetModifiers modifiers)
		{
			return true;
		}

		public bool CanTarget(Actor self, in Target target, ref TargetModifiers modifiers, ref string cursor)
		{
			var forceAttack = modifiers.HasModifier(TargetModifiers.ForceAttack);

			if (target.Type == TargetType.Invalid)
				return false;
			else if (target.Type == TargetType.Terrain && !forceAttack)
				return false;

			var location = self.World.Map.CellContaining(target.CenterPosition);
			if (!self.World.Map.Contains(location))
				return false;

			cursor = this.transforms.CanDeploy(self, location) ? this.deployCursor : this.deployBlockedCursor;
			this.OrderID = forceAttack ? OrderConstants.MoveAndDeployTransformOrderID : DeployTransformOrderID;

			this.IsQueued = modifiers.HasModifier(TargetModifiers.ForceQueue);

			if (target.Type == TargetType.Actor)
				return self == target.Actor;

			return true;
		}

		public bool IsQueued { get; private set; }
	}

	private class MoveToTransform : Activity
	{
		private readonly CPos targetLocation;
		private readonly Transforms transforms;
		private readonly IMove? move;
		private readonly IMoveInfo? moveInfo;

		private int attempt;

		public MoveToTransform(Actor self, CPos targetLocation, Transforms transforms)
		{
			this.targetLocation = targetLocation;
			this.transforms = transforms;
			this.move = self.TraitOrDefault<IMove>();
			this.moveInfo = self.Info.TraitInfo<IMoveInfo>();
		}

		public override bool Tick(Actor self)
		{
			if (this.IsCanceling)
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

			if (this.transforms.ValidateDeployTransform(this.targetLocation, false))
				this.QueueChild(this.transforms.GetTransformActivity(this.targetLocation));

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
