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
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("Allows unit to carry a resource crate.")]
public class CrateTransporterInfo : DockClientBaseInfo
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

public class CrateTransporter : DockClientBase<CrateTransporterInfo>, IRender, INotifyKilled
{
	private readonly Actor actor;
	private readonly CrateTransporterInfo info;
	private ResourceCrate? crate;

	public override BitSet<DockType> GetDockType => this.info.DockingType;

	public CrateTransporter(Actor self, CrateTransporterInfo info)
		: base(self, info)
	{
		this.actor = self;
		this.info = info;
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
		if (this.IsTraitDisabled) return true;

		if (host is ResourceMine resourceMine)
		{
			this.crate = resourceMine.RemoveCrate();
			if (this.crate != null)
				this.crate.SubActor.ParentActor = self;
			else
				return false;
		}
		else if (host is ResourceRefinery resourceRefinery && this.crate != null)
		{
			if (resourceRefinery.Activate(hostActor, this.crate))
				this.crate = null;
			else
				return false;
		}

		return true;
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
					.OffsetBy(this.info.Offset.Rotate(this.actor.Orientation))
					.WithZOffset(this.info.ZOffset)));
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

	void INotifyKilled.Killed(Actor self, AttackInfo e)
	{
		this.crate?.Actor.Trait<ISubActor>()?.OnParentKilled(this.crate.Actor, self);
	}
}
