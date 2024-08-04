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

using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
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

public class SubActor : ISubActor, IFacing, IOccupySpace, ITick, INotifyAddedToWorld, INotifyRemovedFromWorld
{
	private Actor? parentActor;
	private ISubActorParent? subActorParent;
	private IFacing? parentFacing;
	private WPos centerPosition;
	private WRot orientation;
	private CPos location;

	private bool isUnloading;

	public readonly Actor Actor;

	public Actor? ParentActor
	{
		get => this.parentActor;
		set
		{
			if (value == this.parentActor)
				return;

			if (value != null)
				this.centerPosition = value.CenterPosition;

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
			else if (this.Actor.IsInWorld)
				this.AddInfluence();
		});

		this.Facing = init.GetValue<FacingInit, WAngle>(WAngle.Zero);
	}

	private void AddInfluence()
	{
		if (!this.Actor.IsInWorld)
			return;

		this.Actor.World.ActorMap.AddInfluence(this.Actor, this);
	}

	private void RemoveInfluence()
	{
		if (!this.Actor.IsInWorld)
			return;

		this.Actor.World.ActorMap.RemoveInfluence(this.Actor, this);
	}

	void INotifyAddedToWorld.AddedToWorld(Actor self)
	{
		self.World.AddToMaps(self, this);
	}

	void INotifyRemovedFromWorld.RemovedFromWorld(Actor self)
	{
		self.World.RemoveFromMaps(self, this);
	}

	void ITick.Tick(Actor self)
	{
		if (this.parentActor == null || this.isUnloading)
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

	public CPos TopLeft
	{
		get
		{
			if (this.isUnloading)
				return this.location;

			return this.parentActor?.OccupiesSpace?.TopLeft ?? this.Actor.World.Map.CellContaining(this.centerPosition);
		}
	}

	public (CPos, SubCell)[] OccupiedCells()
	{
		if (this.isUnloading)
			return new[] { (this.location, SubCell.FullCell) };

		return this.parentActor?.OccupiesSpace?.OccupiedCells() ?? new[] { (this.location, SubCell.FullCell) };
	}

	void ISubActor.OnParentKilled(Actor self, Actor parentActor)
	{
		this.Actor.Kill(parentActor);
	}

	public void SetLocation(CPos location, WPos? centerPosition = null)
	{
		this.RemoveInfluence();

		this.location = location;
		this.centerPosition = centerPosition.GetValueOrDefault(this.Actor.World.Map.CenterOfCell(location));

		this.AddInfluence();
	}

	internal void OnUnloading(CPos targetLocation)
	{
		this.RemoveInfluence();

		this.location = targetLocation;

		this.centerPosition = this.Actor.World.Map.CenterOfCell(this.location);

		this.Actor.World.AddFrameEndTask(w =>
		{
			if (!this.Actor.IsInWorld)
				w.Add(this.Actor);
		});
		this.isUnloading = true;

		this.AddInfluence();
	}

	internal void OnUnloadCancel()
	{
		this.RemoveInfluence();
		this.isUnloading = false;
		this.Actor.World.AddFrameEndTask(w => w.Remove(this.Actor));
	}

	internal void UnloadComplete()
	{
		this.isUnloading = false;

		this.Actor.World.UpdateMaps(this.Actor, this);
	}
}
