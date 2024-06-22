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

using System.Reflection;
using JetBrains.Annotations;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("Allows unit to carry a resource crate.")]
public class CrateTransporterInfo : DockClientBaseInfo, Requires<IFacingInfo>
{
	[Desc("Docking type.")]
	public readonly BitSet<DockType> DockingType = new("Load", "Unload");

	[Desc("Crate offset.")]
	public readonly WVec Offset;

	[Desc("Crate z offset.")]
	public readonly int ZOffset;

	public override object Create(ActorInitializer init)
	{
		return new CrateTransporter(init.Self, this);
	}
}

public class CrateTransporter : DockClientBase<CrateTransporterInfo>, IRender, ITick, INotifyAddedToWorld
{
	private readonly CrateTransporterInfo info;

	private readonly IFacing facing;

	private ResourceCrate? crate;

	public override BitSet<DockType> GetDockType => this.info.DockingType;

	public CrateTransporter(Actor self, CrateTransporterInfo info)
		: base(self, info)
	{
		this.info = info;
		this.facing = self.TraitOrDefault<IFacing>();
	}

	void INotifyAddedToWorld.AddedToWorld(Actor self)
	{
		// TODO temp for testing.
		//this.crate = self.World.CreateActor(
		//		false,
		//		"crate",
		//		new TypeDictionary { new ParentActorInit(self), new LocationInit(self.Location), new OwnerInit(self.Owner) }
		//	)
		//	.Trait<ResourceCrate>();
	}

	// Need to override CanDockAt, since we need to know status of the host (either Mine or Refinery)
	public override bool CanDockAt(Actor hostActor, IDockHost host, bool forceEnter = false, bool ignoreOccupancy = false)
	{
		if (!base.CanDockAt(hostActor, host, forceEnter, ignoreOccupancy))
			return false;

		if (host is ResourceMine)
			return this.crate == null;
		else
			return this.crate != null;
	}

	public override bool OnDockTick(Actor self, Actor hostActor, IDockHost host)
	{
		if (this.IsTraitDisabled) return true;

		if (host is ResourceMine resourceMine)
		{
			this.crate = resourceMine.RemoveCrate(hostActor);
		}

		return true;
	}

	void ITick.Tick(Actor self)
	{
		this.UpdateCratePositionAndRotation(self.CenterPosition + this.info.Offset.Rotate(this.facing.Orientation), this.facing.Facing);
	}

	private void UpdateCratePositionAndRotation(WPos position, WAngle facing)
	{
		if (this.crate == null)
			return;

		foreach (var mobile in this.crate.Actor.TraitsImplementing<Mobile>())
		{
			mobile.Facing = facing;

			typeof(Mobile).GetProperty("CenterPosition", BindingFlags.Instance | BindingFlags.Public)?.SetValue(mobile, position);
			typeof(Mobile).GetField("oldFacing", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(mobile, mobile.Facing);
			typeof(Mobile).GetField("oldPos", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(mobile, mobile.CenterPosition);
		}
	}

	IEnumerable<IRenderable> IRender.Render(Actor self, WorldRenderer wr)
	{
		var result = new List<IRenderable>();

		if (this.crate == null)
			return result;

		foreach (var render in this.crate.Actor.TraitsImplementing<IRender>())
			result.AddRange(render.Render(this.crate.Actor, wr).Select(e => e.WithZOffset(this.info.ZOffset)));

		return result;
	}

	IEnumerable<Rectangle> IRender.ScreenBounds(Actor self, WorldRenderer wr)
	{
		var result = new List<Rectangle>();

		if (this.crate == null)
			return result;

		foreach (var render in this.crate.Actor.TraitsImplementing<IRender>())
			result.AddRange(render.ScreenBounds(this.crate.Actor, wr));

		return result;
	}
}
