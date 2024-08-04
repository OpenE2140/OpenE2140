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
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.OpenE2140.Activites.Resources;
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

	[VoiceReference]
	[Desc("Voice to be played when ordered to unload.")]
	public readonly string UnloadVoice = "Action";

	[CursorReference]
	[Desc("Cursor to display when unloading crate.")]
	public readonly string CrateUnloadCursor = "deliver";

	[CursorReference]
	[Desc("Cursor to display when unloading crate.")]
	public readonly string CrateUnloadBlockedCursor = "generic-blocked";

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

public class CrateTransporter : DockClientBase<CrateTransporterInfo>, IRender, ISubActorParent, INotifyKilled, IResolveOrder, IOrderVoice, IIssueOrder, ITick
{
	private const string UnloadResourceCrateOrderID = "UnloadResourceCrate";

	private readonly Actor actor;
	private readonly CrateTransporterInfo info;
	private ResourceCrate? crate;

	private int respawnTick;

	public override BitSet<DockType> GetDockType => this.info.DockingType;

	public CrateTransporter(ActorInitializer init, CrateTransporterInfo info)
		: base(init.Self, info)
	{
		this.actor = init.Self;
		this.info = info;

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

	WVec ISubActorParent.SubActorOffset => this.info.Offset.Rotate(this.actor.Orientation);

	public override bool CanDockAt(Actor hostActor, IDockHost host, bool forceEnter = false, bool ignoreOccupancy = false)
	{
		if (!base.CanDockAt(hostActor, host, forceEnter, ignoreOccupancy))
			return false;

		if (host is ResourceMine)
			return this.crate == null;
		else if (host is ResourceRefinery)
			return this.crate != null;

		return false;
	}

	public override bool OnDockTick(Actor self, Actor hostActor, IDockHost host)
	{
		//return false;
		if (this.IsTraitDisabled) return true;

		if (host is ResourceMine resourceMine)
		{
			this.crate = resourceMine.RemoveCrate(hostActor);
			if (this.crate != null)
				this.crate.SubActor.ParentActor = self;
		}
		else if (host is ResourceRefinery resourceRefinery && this.crate != null)
		{
			this.crate.SubActor.ParentActor = null;
			resourceRefinery.Activate(hostActor, this.crate);
			this.crate = null;
			//this.respawnTick = 100;
		}

		return true;
	}

	internal bool CanUnloadAt(Actor self, CPos targetLocation)
	{
		return !self.World.ActorMap.AnyActorsAt(targetLocation, SubCell.FullCell, a => a != self);
	}

	internal void ReserveUnloadLocation(CPos targetLocation)
	{
		if (this.crate == null)
			return;

		this.crate.SubActor.OnUnloading(targetLocation);
	}

	internal void CancelUnload()
	{
		this.crate?.SubActor.OnUnloadCancel();
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
			result.AddRange(render.Render(this.crate.Actor, wr).Select(e => e.WithZOffset(this.info.ZOffset)));

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

	public ResourceCrate? UnloadCrate(Actor self)
	{
		var crate = this.crate;
		if (crate != null)
		{
			this.crate = null;

			crate.SubActor.ParentActor = null;
		}

		return crate;
	}

	void IResolveOrder.ResolveOrder(Actor self, Order order)
	{
		if (order.OrderString == UnloadResourceCrateOrderID)
		{
			var target = order.Target;
			if (target.Type != TargetType.Actor)
				return;

			if (this.crate == null)
				return;

			self.QueueActivity(new CrateUnload(self, self.Location));
		}
	}

	string? IOrderVoice.VoicePhraseForOrder(Actor self, Order order)
	{
		if (this.IsTraitDisabled)
			return null;

		if (order.OrderString == UnloadResourceCrateOrderID) // && CanDockAt(order.Target.Actor, false, true))
			return this.info.UnloadVoice;

		return null;
	}

	IEnumerable<IOrderTargeter> IIssueOrder.Orders
	{
		get
		{
			yield return new DeployOrderTargeter(UnloadResourceCrateOrderID, 5, () => this.crate != null ? this.info.CrateUnloadCursor : this.info.CrateUnloadBlockedCursor);
		}
	}

	Order? IIssueOrder.IssueOrder(Actor self, IOrderTargeter order, in Target target, bool queued)
	{
		if (order.OrderID == UnloadResourceCrateOrderID)
			return new Order(order.OrderID, self, target, queued);

		return null;
	}

    void ITick.Tick(Actor self)
    {
        if (this.crate != null)
            return;

        //if (--this.respawnTick <= 0)
        //{
        //	var crateActor = self.World.CreateActor(
        //		false,
        //		this.info.CrateActor,
        //		new TypeDictionary
        //		{
        //			new ParentActorInit(this.actor),
        //			new LocationInit(this.actor.Location),
        //			new OwnerInit(this.actor.Owner)
        //		});
        //	this.crate = crateActor.Trait<ResourceCrate>();
        //	this.crate.SubActor.ParentActor = this.actor;
        //}
    }
}
