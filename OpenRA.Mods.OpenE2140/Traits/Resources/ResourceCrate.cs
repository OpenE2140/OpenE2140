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
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

public interface ISubActorParent
{
	WVec SubActorOffset { get; }
}

[UsedImplicitly]
[Desc("This actor is a resource crate.")]
public class ResourceCrateInfo : TraitInfo, IFacingInfo
{
	public WAngle GetInitialFacing() => WAngle.Zero;

	public override object Create(ActorInitializer init)
	{
		return new ResourceCrate(init);
	}
}

public class ResourceCrate : IFacing, IOccupySpace, ITick
{
	private Actor? parentActor;
	private ISubActorParent? subActorParent;
	private IFacing? parentFacing;
	private WPos centerPosition;
	private WRot orientation;
	private CPos location;

	public readonly Actor Actor;
	public int Resources;

	public Actor? ParentActor
	{
		get => this.parentActor;
		set
		{
			if (value == null)
			{
				this.Actor.World.ActorMap.AddInfluence(this.Actor, this);
			}
			else
			{
				this.centerPosition = value.CenterPosition;
				this.Actor.World.ActorMap.RemoveInfluence(this.Actor, this);
			}

			this.parentActor = value;
			this.subActorParent = value?.TraitOrDefault<ISubActorParent>();

			this.parentFacing = value?.TraitOrDefault<IFacing>();
		}
	}

	public ResourceCrate(ActorInitializer init)
	{
		this.Actor = init.Self;

		var locationInit = init.GetOrDefault<LocationInit>();
		if (locationInit != null)
		{
			this.location = locationInit.Value;
			this.centerPosition = init.World.Map.CenterOfCell(this.location);
		}

		var parentInit = init.GetOrDefault<ParentActorInit>();
		if (parentInit != null)
		{
			init.World.AddFrameEndTask(w =>
			{
				var parentActor = parentInit.Value.Actor(w).Value;
				if (this.Actor.IsInWorld && parentActor.IsInWorld)
					this.ParentActor = parentActor;
			});
		}

		this.Facing = init.GetValue<FacingInit, WAngle>(WAngle.Zero);
	}

	void ITick.Tick(Actor self)
	{
		if (this.parentActor == null)
		{
			return;
		}

		this.orientation = this.parentActor.Orientation;
		this.centerPosition = this.parentActor.CenterPosition;
		this.location = this.parentActor.Location;
	}

	#region IFacing

	[Sync]
	public WAngle Facing
	{
		get => this.parentActor?.Orientation.Yaw ?? this.orientation.Yaw;
		set
		{
			if (this.parentActor == null || this.parentFacing == null)
			{
				this.orientation = this.orientation.WithYaw(value);
			}
			else
			{
				this.parentFacing.Facing = value;
				this.orientation = this.parentFacing.Orientation;
			}
		}
	}

	public WRot Orientation => this.parentActor?.Orientation ?? this.orientation;

	public WAngle TurnSpeed => WAngle.Zero;

	#endregion

	#region IOccupySpace

	[Sync]
	public WPos CenterPosition
	{
		get
		{
			if (this.parentActor == null || this.subActorParent == null)
				return this.centerPosition;

			return this.parentActor.CenterPosition + this.subActorParent.SubActorOffset;
		}
	}

	public CPos TopLeft => this.parentActor?.OccupiesSpace?.TopLeft ?? this.Actor.World.Map.CellContaining(this.centerPosition);

	public (CPos, SubCell)[] OccupiedCells()
	{
		return this.parentActor?.OccupiesSpace?.OccupiedCells() ?? new[] { (this.location, SubCell.FullCell) };
	}

	#endregion
}
