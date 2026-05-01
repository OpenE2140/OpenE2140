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
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Common.Traits;
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

	public bool CanDeploy(Actor self)
	{
		if (this.IsTraitPaused || this.IsTraitDisabled)
			return false;

		return this.customBuildingInfo?.CanPlaceBuilding(self.World, self.Location + this.Info.Offset, self) != false;
	}

	private IEnumerable<Order> ClearBlockersOrders(CPos topLeft)
	{
		return this.customBuildingInfo == null
			? []
			: AIUtils.ClearBlockersOrders(this.customBuildingInfo.Tiles(topLeft).ToList(), this.self.Owner, this.self);
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
			Faction = this.faction
		};
	}

	public IEnumerable<IOrderTargeter> Orders
	{
		get
		{
			if (!this.IsTraitDisabled)
			{
				yield return new DeployOrderTargeter(DeployTransformOrderID, 5,
					() => this.CanDeploy(this.self) ? this.Info.DeployCursor : this.Info.DeployBlockedCursor);
			}
		}
	}

	public Order? IssueOrder(Actor self, IOrderTargeter order, in Target target, bool queued)
	{
		if (order.OrderID is DeployTransformOrderID or OrderConstants.MoveAndDeployTransformOrderID)
			return new Order(order.OrderID, self, queued);

		return null;
	}

	Order IIssueDeployOrder.IssueDeployOrder(Actor self, bool queued)
	{
		return new Order(DeployTransformOrderID, self, queued);
	}

	bool IIssueDeployOrder.CanIssueDeployOrder(Actor self, bool queued) { return !this.IsTraitPaused && !this.IsTraitDisabled; }

	public void DeployTransform(bool queued)
	{
		if (!queued && !this.CanDeploy(this.self))
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

	void IResolveOrder.ResolveOrder(Actor self, Order order)
	{
		if (this.IsTraitPaused || this.IsTraitDisabled)
			return;

		if (order.OrderString == DeployTransformOrderID)
			this.DeployTransform(order.Queued);
		else if (order.OrderString == OrderConstants.MoveAndDeployTransformOrderID)
		{
			// Only terrain target is supported.
			if (order.Target.Type != TargetType.Terrain)
			{
				this.DeployTransform(order.Queued);
				return;
			}

			// If at target location already, just queue Transform activity directly.
			var targetLocation = self.World.Map.CellContaining(order.Target.CenterPosition);
			if (targetLocation == self.Location)
			{
				this.DeployTransform(order.Queued);
				return;
			}

			self.QueueActivity(order.Queued, new MoveToTransform(self, targetLocation, this));
			self.ShowTargetLines();
		}
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

		protected override void OnFirstRun(Actor self)
		{
			if (self.Location != this.targetLocation && this.move == null)
				this.Cancel(self);
		}

		public override bool Tick(Actor self)
		{
			if (this.move == null || this.IsCanceling)
				return true;

			if (self.Location != this.targetLocation)
			{
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

			this.transforms.DeployTransform(false);

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
