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
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.SubActors;

public class SubActorInfo : TraitInfo, IFacingInfo, IOccupySpaceInfo, IEditorActorOptions
{
	[GrantedConditionReference]
	[Desc("The condition to grant to self when the subactor is attached.")]
	public readonly string? AttachedCondition;

	[Desc("Display order for the facing slider in the map editor.")]
	public readonly int EditorFacingDisplayOrder = 3;

	[Desc("Maximum number of facings that the subactor is allowed to have, when it is not attached to any actor.")]
	public readonly int MaximumFacingsWhenDetached = 16;

	public WAngle GetInitialFacing() => WAngle.Zero;

	public override object Create(ActorInitializer init)
	{
		return new SubActor(init, this);
	}

	bool IOccupySpaceInfo.SharesCell => false;

	IReadOnlyDictionary<CPos, SubCell> IOccupySpaceInfo.OccupiedCells(ActorInfo info, CPos location, SubCell subCell)
	{
		return new Dictionary<CPos, SubCell> { { location, subCell } };
	}

	IEnumerable<EditorActorOption> IEditorActorOptions.ActorOptions(ActorInfo ai, OpenRA.World world)
	{
		yield return new EditorActorSlider("Facing", this.EditorFacingDisplayOrder, 0, 1023, 0,
			actor =>
			{
				var init = actor.GetInitOrDefault<FacingInit>(this);
				return (init?.Value ?? this.GetInitialFacing()).Angle;
			},
			(actor, value) => actor.ReplaceInit(new FacingInit(Util.QuantizeFacing(new WAngle((int)value), this.MaximumFacingsWhenDetached))));
	}
}

public class SubActor : ISubActor, IFacing, IOccupySpace, ITick, INotifyAddedToWorld, INotifyRemovedFromWorld
{
	private Actor? parentActor;
	private IFacing? parentFacing;
	private WPos centerPosition;
	private WRot orientation;
	private CPos location;
	private int attachedToken = Actor.InvalidConditionToken;

	public readonly Actor Actor;
	private readonly SubActorInfo info;

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
			}
			else
			{
				this.parentFacing = null;
			}

			if (!string.IsNullOrEmpty(this.info.AttachedCondition))
				this.Actor.GrantOrRevokeCondition(ref this.attachedToken, this.parentActor != null, this.info.AttachedCondition);
		}
	}

	public SubActor(ActorInitializer init, SubActorInfo info)
	{
		this.Actor = init.Self;
		this.info = info;

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
			{
				this.Facing = new WAngle(init.GetValue<FacingInit, WAngle>(info.GetInitialFacing()).Angle);

				this.AddInfluence();
			}
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
		if (this.parentActor == null)
		{
			return;
		}

		this.orientation = this.parentActor.Orientation;
		this.centerPosition = this.parentActor.CenterPosition;
		this.location = this.parentActor.Location;
	}

	[VerifySync]
	public WAngle Facing
	{
		get => this.parentActor?.Orientation.Yaw ?? this.orientation.Yaw;
		set
		{
			if (this.parentActor == null || this.parentFacing == null)
			{
				this.orientation = this.orientation.WithYaw(Util.QuantizeFacing(value, this.info.MaximumFacingsWhenDetached));
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

	[VerifySync]
	public WPos CenterPosition
	{
		get
		{
			return this.parentActor == null ? this.centerPosition : this.parentActor.CenterPosition;
		}
	}

	public CPos TopLeft => this.parentActor?.OccupiesSpace?.TopLeft ?? this.Actor.World.Map.CellContaining(this.centerPosition);

	public (CPos, SubCell)[] OccupiedCells()
	{
		return this.parentActor?.OccupiesSpace?.OccupiedCells() ?? [(this.location, SubCell.FullCell)];
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

	internal void UnloadComplete()
	{
		this.RemoveInfluence();

		this.Actor.World.AddFrameEndTask(w =>
		{
			if (!this.Actor.IsInWorld)
			{
				w.Add(this.Actor);

				this.AddInfluence();

				this.Actor.World.UpdateMaps(this.Actor, this);
			}
		});
	}

	internal void LoadComplete()
	{
		this.Actor.World.RemoveFromMaps(this.Actor, this);

		this.Actor.World.AddFrameEndTask(w =>
		{
			if (this.Actor.IsInWorld)
				w.Remove(this.Actor);
		});
	}
}
