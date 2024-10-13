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
using OpenRA.Activities;
using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.OpenE2140.Traits.Resources.Activities;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("Allows unit to carry a resource crate.")]
public class CrateTransporterInfo : DockClientBaseInfo, IEditorActorOptions, IRenderActorPreviewSpritesInfo
{
	[Desc("Docking type.")]
	public readonly BitSet<DockType> DockingType = new("Load", "Unload");

	[Desc("Crate offset.")]
	public readonly WVec Offset;

	[Desc("Crate z offset.")]
	public readonly int ZOffset;

	[Desc("Display order for the initial resources slider in the map editor.")]
	public readonly int EditorInitialResourcesDisplayOrder = 3;

	[Desc("Maximum amount of the initial resources slider in the map editor.")]
	public readonly int EditorMaximumInitialResourcesDisplayOrder = 500;

	[SequenceReference]
	[Desc("Displayed when docking to refinery.")]
	public readonly string DockSequence = "dock";

	[SequenceReference]
	[Desc("Looped while unloading at refinery.")]
	public readonly string DockLoopSequence = "dock-loop";

	[VoiceReference]
	[Desc("Voice to be played when ordered to unload.")]
	public readonly string UnloadVoice = "Action";

	[Desc("Percentage modifier to apply to movement speed while docking to conveyor belt or (un)loading crate to/from ground.")]
	public readonly int DockSpeedModifier = 70;

	[CursorReference]
	[Desc("Cursor to display when unloading crate.")]
	public readonly string CrateUnloadCursor = "deliver";

	[CursorReference]
	[Desc("Cursor to display when loading crate.")]
	public readonly string CrateLoadCursor = "pickup";

	[CursorReference]
	[Desc("Cursor to display when loading or unloading crate is not possible.")]
	public readonly string CrateLoadUnloadBlockedCursor = "generic-blocked";

	[Desc("The resource crate actor. Make sure it's the same for ResourceMine actor.")]
	public readonly string CrateActor = "crate";

	public override object Create(ActorInitializer init)
	{
		return new CrateTransporter(init, this);
	}

	IEnumerable<IActorPreview> IRenderActorPreviewSpritesInfo.RenderPreviewSprites(ActorPreviewInitializer init, string image, int facings, PaletteReference p)
	{
		if (!this.EnabledByDefault)
			yield break;

		var resourceInit = init.GetOrDefault<ResourcesInit>();
		if (resourceInit == null || resourceInit.Value == 0 || this.CrateActor == null)
			yield break;

		var body = init.Actor.TraitInfo<BodyOrientationInfo>();
		var crateActor = init.World.Map.Rules.Actors[this.CrateActor];
		image = crateActor.TraitInfo<RenderSpritesInfo>().GetImage(crateActor, init.GetValue<FactionInit, string>(this));

		var facing = init.GetFacing();

		var anim = new Animation(init.World, image, () => body.QuantizeFacing(facing(), facings));
		anim.Play("idle");

		yield return new SpriteActorPreview(
			anim,
			() => this.Offset.Rotate(body.QuantizeOrientation(WRot.FromYaw(facing()), facings)),
			() => this.ZOffset,
			null);
	}

	IEnumerable<EditorActorOption> IEditorActorOptions.ActorOptions(ActorInfo ai, OpenRA.World world)
	{
		yield return new EditorActorSlider("Resources", this.EditorInitialResourcesDisplayOrder, 0, this.EditorMaximumInitialResourcesDisplayOrder, 20,
			actor =>
			{
				var init = actor.GetInitOrDefault<ResourcesInit>(this);
				if (init != null)
					return init.Value;

				return 0;
			},
			(actor, value) =>
			{
				if (value > 0)
					actor.ReplaceInit(new ResourcesInit((int)value), this);
				else
					actor.RemoveInit<ResourcesInit>();
			});
	}
}

public class CrateTransporter : DockClientBase<CrateTransporterInfo>, IRender, INotifyKilled, IResolveOrder, IOrderVoice, IIssueOrder, IIssueDeployOrder
{
	private const string UnloadResourceCrateOrderID = "UnloadResourceCrate";
	private const string LoadResourceCrateOrderID = "LoadResourceCrate";

	private readonly Actor actor;
	private readonly CrateTransporterInfo info;
	private readonly Mobile? mobile;
	private ResourceCrate? crate;
	private bool? dockingInProgress;

	public override BitSet<DockType> GetDockType => this.info.DockingType;

	public WVec CrateOffset { get; set; }

	public CrateTransporter(ActorInitializer init, CrateTransporterInfo info)
		: base(init.Self, info)
	{
		this.actor = init.Self;
		this.info = info;
		this.mobile = this.actor.TraitOrDefault<Mobile>();

		var resourcesInit = init.GetOrDefault<ResourcesInit>();
		if (resourcesInit != null && resourcesInit.Value > 0)
		{
			init.World.AddFrameEndTask(w =>
			{
				var crateActor = w.CreateActor(
					false,
					this.info.CrateActor,
					new TypeDictionary
					{
						new ParentActorInit(this.actor),
						new LocationInit(this.actor.Location),
						new OwnerInit(this.actor.Owner),
						resourcesInit
					});
				this.crate = crateActor.Trait<ResourceCrate>();
				this.crate.SubActor.ParentActor = this.actor;
			});
		}
	}

	public override bool CanDockAt(Actor hostActor, IDockHost host, bool forceEnter = false, bool ignoreOccupancy = false)
	{
		if (!base.CanDockAt(hostActor, host, forceEnter, ignoreOccupancy))
			return false;

		if (hostActor.Info.HasTraitInfo<ResourceMineInfo>())
			return this.crate == null;
		else if (hostActor.Info.HasTraitInfo<ResourceRefineryInfo>())
			return this.crate != null;

		return false;
	}

	public override bool OnDockTick(Actor self, Actor hostActor, IDockHost host)
	{
		if (this.IsTraitDisabled)
			return true;

		if (this.dockingInProgress == null)
		{
			var currentActivity = self.CurrentActivity.ActivitiesImplementing<Activity>().First();
			currentActivity.QueueChild(new ConveyorBeltLoadUnloadCrate(self, (IConveyorBeltDockHost)host, hostActor));

			this.dockingInProgress = true;
		}

		return !this.dockingInProgress.Value;
	}

	public override void OnDockCompleted(Actor self, Actor hostActor, IDockHost host)
	{
		this.dockingInProgress = null;
	}

	internal bool OnConveyorBeltDockTick(Actor self, ConveyorBelt conveyorBelt, Actor conveyorBeltActor)
	{
		if (conveyorBelt is ResourceMine resourceMine)
		{
			this.crate = resourceMine.RemoveCrate();
			if (this.crate != null)
				this.crate.SubActor.ParentActor = self;
			else
				return false;
		}
		else if (conveyorBelt is ResourceRefinery resourceRefinery && this.crate != null)
		{
			if (resourceRefinery.Activate(conveyorBeltActor, this.crate))
				this.crate = null;
			else
				return false;
		}

		return true;
	}

	internal void OnConveyorBeltUndock()
	{
		this.dockingInProgress = false;
	}

	public bool CanUnload()
	{
		return this.crate != null;
	}

	public bool CanLoad(Actor crateActor)
	{
		return !crateActor.Disposed && crateActor.IsInWorld && crateActor.Info.HasTraitInfo<ResourceCrateInfo>();
	}

	internal bool CanUnloadAt(Actor self, CPos targetLocation)
	{
		return !self.World.ActorMap.AnyActorsAt(targetLocation, SubCell.FullCell, a => a != self);
	}

	internal void ReserveUnloadLocation(CPos targetLocation)
	{
		if (this.crate == null)
			return;

		// We need to block target cell (until the unload is complete) so that no other actor can enter it while the unloading is in progress.
		// Current solution makes use of CrateTransporter's Mobile implementing IOccupySpace by returning both FromCell and ToCell (if they differ).

		// Unfortunately this hacky solution is necessary. If the cell is to be blocked by the crate itself,
		// it's necessary to add the crate actor to the world and that causes more issues (when it comes to the crate unload feature).
		// So the crate is added to world at the very last moment: i.e. when the crate (SubActor) is detached from CrateTransporter.
		this.mobile?.SetLocation(targetLocation, SubCell.FullCell, this.mobile.ToCell, SubCell.FullCell);
	}

	internal void UnloadComplete()
	{
		this.mobile?.SetLocation(this.mobile.ToCell, SubCell.FullCell, this.mobile.ToCell, SubCell.FullCell);
	}

	void INotifyKilled.Killed(Actor self, AttackInfo e)
	{
		this.crate?.Actor.Trait<ISubActor>()?.OnParentKilled(this.crate.Actor, self);
	}

	IEnumerable<IRenderable> IRender.Render(Actor self, WorldRenderer wr)
	{
		var result = new List<IRenderable>();

		if (this.crate == null || this.crate.Actor.Disposed)
			return result;

		foreach (var render in this.crate.Actor.TraitsImplementing<IRender>())
		{
			result.AddRange(
				render.Render(this.crate.Actor, wr)
				.Select(e => e
					.OffsetBy((this.info.Offset + this.CrateOffset).Rotate(this.actor.Orientation))
					.WithZOffset(this.info.ZOffset * 4)));
		}

		return result;
	}

	IEnumerable<Rectangle> IRender.ScreenBounds(Actor self, WorldRenderer wr)
	{
		var result = new List<Rectangle>();

		if (this.crate == null || this.crate.Actor.Disposed)
			return result;

		foreach (var render in this.crate.Actor.TraitsImplementing<IRender>())
			result.AddRange(render.ScreenBounds(this.crate.Actor, wr));

		return result;
	}

	public void UnloadCrate(CPos targetLocation)
	{
		if (this.crate == null)
			return;

		this.crate.SubActor.SetLocation(targetLocation);
		this.crate.SubActor.UnloadComplete();

		this.UnloadComplete();

		this.crate.SubActor.ParentActor = null;

		this.crate = null;
	}

	public void LoadCrate(Actor self, Actor crateActor)
	{
		this.crate = crateActor.Trait<ResourceCrate>();
		this.crate.SubActor.LoadComplete();
		this.crate.SubActor.ParentActor = self;
	}

	void IResolveOrder.ResolveOrder(Actor self, Order order)
	{
		if (order.OrderString == UnloadResourceCrateOrderID)
		{
			if (!order.Queued && !this.CanUnload())
				return;

			self.QueueActivity(order.Queued, new CrateUnload(self));
		}
		else if (order.OrderString == LoadResourceCrateOrderID)
		{
			if (!order.Queued && order.Target.Type == TargetType.Actor && !this.CanLoad(order.Target.Actor))
				return;

			self.QueueActivity(order.Queued, this.mobile != null ? new MobileCrateLoad(self, order.Target) : new AircraftCrateLoad(self, order.Target));
		}
	}

	string? IOrderVoice.VoicePhraseForOrder(Actor self, Order order)
	{
		if (this.IsTraitDisabled)
			return null;

		if (order.OrderString == UnloadResourceCrateOrderID)
			return this.info.UnloadVoice;

		if (order.OrderString == LoadResourceCrateOrderID)
			return this.info.UnloadVoice;

		return null;
	}

	IEnumerable<IOrderTargeter> IIssueOrder.Orders
	{
		get
		{
			if (this.crate != null)
				yield return new DeployOrderTargeter(UnloadResourceCrateOrderID, 5, () => this.crate != null ? this.info.CrateUnloadCursor : this.info.CrateLoadUnloadBlockedCursor);
			else
				yield return new CrateLoadTargeter(this, LoadResourceCrateOrderID, 5);
		}
	}

	Order? IIssueOrder.IssueOrder(Actor self, IOrderTargeter order, in Target target, bool queued)
	{
		if (order.OrderID is UnloadResourceCrateOrderID or LoadResourceCrateOrderID)
			return new Order(order.OrderID, self, target, queued);

		return null;
	}

	Order IIssueDeployOrder.IssueDeployOrder(Actor self, bool queued)
	{
		return new Order(UnloadResourceCrateOrderID, self, queued);
	}

	bool IIssueDeployOrder.CanIssueDeployOrder(Actor self, bool queued) { return !this.IsTraitDisabled && (queued || this.crate != null); }

	private class CrateLoadTargeter : UnitOrderTargeter
	{
		private readonly CrateTransporter crateTransporter;

		public CrateLoadTargeter(CrateTransporter crateTransporter, string order, int priority)
			: base(order, priority, crateTransporter.Info.CrateLoadCursor, true, true)
		{
			this.crateTransporter = crateTransporter;
		}

		public override bool CanTargetActor(Actor self, Actor target, TargetModifiers modifiers, ref string cursor)
		{
			cursor = this.crateTransporter.Info.CrateLoadCursor;
			return target.Info.HasTraitInfo<ResourceCrateInfo>();
		}

		public override bool CanTargetFrozenActor(Actor self, FrozenActor target, TargetModifiers modifiers, ref string cursor)
		{
			// Resource crates are hidden beneath fog, so they can never be frozen.
			return false;
		}
	}
}
