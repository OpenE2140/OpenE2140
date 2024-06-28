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

using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.SubActors;

public class SubActorInfo : TraitInfo, IFacingInfo
{
	public WAngle GetInitialFacing() => WAngle.Zero;

	public override object Create(ActorInitializer init)
	{
		return new SubActor(init);
	}
}

public class SubActor : ISubActor, IFacing, IOccupySpace, ITick
{
	private Actor? parentActor;
	private ISubActorParent? subActorParent;
	private IFacing? parentFacing;
	private WPos centerPosition;
	private WRot orientation;
	private CPos location;

	public readonly Actor Actor;

	public Actor? ParentActor
	{
		get => this.parentActor;
		set
		{
			if (value == this.parentActor)
				return;

			if (value == null)
			{
				this.AddToMap();
			}
			else
			{
				this.centerPosition = value.CenterPosition;
				this.RemoveFromMap();
			}

			this.parentActor = value;
			if (this.parentActor != null)
			{
				this.parentFacing = this.parentActor.TraitOrDefault<IFacing>();
				this.subActorParent = this.parentActor.TraitOrDefault<ISubActorParent>();
			}
			else
			{
				this.parentFacing = null;
				this.subActorParent = null;
			}
		}
	}

	public SubActor(ActorInitializer init)
	{
		this.Actor = init.Self;

		var locationInit = init.GetOrDefault<LocationInit>();
		if (locationInit != null)
		{
			this.location = locationInit.Value;
			this.centerPosition = init.World.Map.CenterOfCell(this.location);
		}

		var parentInit = init.GetOrDefault<ParentActorInit>();

		init.World.AddFrameEndTask(w =>
		{
			var parentActor = parentInit?.Value.Actor(w).Value;
			if (this.Actor.IsInWorld && parentActor?.IsInWorld == true)
				this.ParentActor = parentActor;
			else
				this.AddToMap();
		});

		this.Facing = init.GetValue<FacingInit, WAngle>(WAngle.Zero);
	}

	private void AddToMap()
	{
		this.Actor.World.ActorMap.AddInfluence(this.Actor, this);
		this.Actor.World.ActorMap.AddPosition(this.Actor, this);
	}

	private void RemoveFromMap()
	{
		this.Actor.World.ActorMap.RemoveInfluence(this.Actor, this);
		this.Actor.World.ActorMap.RemovePosition(this.Actor, this);
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

	void ISubActor.OnParentKilled(Actor self, Actor parentActor)
	{
		this.Actor.Kill(parentActor);
	}
}
