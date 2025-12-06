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
using OpenRA.Mods.OpenE2140.Activites.Move;
using OpenRA.Mods.OpenE2140.Extensions;

namespace OpenRA.Mods.OpenE2140.Traits.Production;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("Special Production trait that forces produced actors to move to specified waypoint before moving onto exit cell.")]
public class ExitWaypointProductionInfo : AnimatedExitProductionInfo
{
	[Desc($"Position along which produced actor needs to pass before it can move to exit cell. When specified, overrides value specified in {nameof(ExitWaypoint)}")]
	public readonly WVec? ExitWaypointOffset;

	[Desc("Cell along which produced actor needs to pass before it can move to exit cell. Cell is relative to cell, on which the produced actor is spawned.")]
	public readonly CVec? ExitWaypoint;

	public override object Create(ActorInitializer init)
	{
		return new ExitWaypointProduction(init, this);
	}
}

public class ExitWaypointProduction : AnimatedExitProduction, ISafeDragNotify
{
	private enum CustomProductionState
	{
		None, MovingToExitWaypoint
	}

	private readonly ExitWaypointProductionInfo info;
	private CustomProductionState customState = CustomProductionState.None;

	public ExitWaypointProduction(ActorInitializer init, ExitWaypointProductionInfo info)
		: base(init, info)
	{
		this.info = info;
	}

	protected override WPos GetSpawnPosition(Actor self, CPos exitCell)
	{
		return base.GetSpawnPosition(self, exitCell) + (this.productionInfo?.ExitInfo.SpawnOffset ?? WVec.Zero);
	}

	protected override void Eject(Actor self)
	{
		if (this.productionInfo == null)
			return;

		if (this.GetWaypointPosition(self) is not WPos waypointPosition)
		{
			base.Eject(self);

			return;
		}

		var exit = self.Location + this.productionInfo.ExitInfo.ExitCell;
		var spawnPosition = this.GetSpawnPosition(self, exit);
		var initialFacing = AnimatedExitProduction.GetInitialFacing(this.productionInfo.Producee, spawnPosition, waypointPosition);

		var inits = this.productionInfo.Inits;
		inits.Add(new LocationInit(this.GetSpawnCell(self)));
		inits.Add(new FacingInit(initialFacing));

		this.DoProductionBase(self, this.productionInfo.Producee, null, this.productionInfo.ProductionType, inits);
	}

	protected override void TickCustom(Actor self)
	{
		switch (this.customState)
		{
			case CustomProductionState.MovingToExitWaypoint:
			{
				var actor = this.productionInfo?.Actor;

				if (this.productionInfo == null || actor == null || actor.IsInWorld == false)
				{
					// actor was likely destroyed, close exit
					this.productionInfo = null;
					this.customState = CustomProductionState.None;

					this.Close(self);
				}

				break;
			}

			case CustomProductionState.None:
			default:
				throw new ArgumentOutOfRangeException();
		}
	}

	protected override void OnUnitProduced(Actor self, Actor other, CPos exit)
	{
		base.OnUnitProduced(self, other, exit);

		if (this.GetWaypointPosition(self) is not WPos waypointPosition)
			return;

		if (!other.TryGetTrait<Mobile>(out var mobile))
			return;

		mobile.SetCenterPosition(other, this.GetSpawnPosition(self, exit));

		this.State = AnimationState.Custom;
		this.customState = CustomProductionState.MovingToExitWaypoint;

		other.QueueActivity(new ProductionExitMove(other, self, waypointPosition, 50));
	}


	private WPos? GetWaypointPosition(Actor self)
	{
		if (this.info.ExitWaypointOffset != null)
			return self.CenterPosition + this.info.Position + this.info.ExitWaypointOffset;
		else if (this.info.ExitWaypoint != null)
			return self.World.Map.CenterOfCell(this.GetSpawnCell(self) + this.info.ExitWaypoint.Value);
		return null;
	}

	void ISafeDragNotify.SafeDragFailed(Actor self, Actor movingActor)
	{
		// TODO: should retry logic be here ???
	}

	void ISafeDragNotify.SafeDragComplete(Actor self, Actor movingActor)
	{
		// Produced actor might still be moving along the exit path, so don't do anything, if it does.
		if (this.customState != CustomProductionState.MovingToExitWaypoint || movingActor != this.productionInfo?.Actor)
			return;

		this.customState = CustomProductionState.None;

		// It's necessary to bypass AnimatedExitProduction here and move State and queue ProductionExitMove now.
		// The reason is that if we delay this until next tick of AnimatedExitProduction, produced actor stops for one tick (which is undesirable).
		this.State = AnimationState.Ejecting;

		var actor = this.productionInfo.Actor;

		var end = self.World.Map.CenterOfCell(self.Location + this.productionInfo.ExitInfo.ExitCell);
		this.productionInfo = this.productionInfo with { ExitMoveActivity = new ProductionExitMove(actor, self, end) };
		actor.QueueActivity(this.productionInfo.ExitMoveActivity);
	}
}
