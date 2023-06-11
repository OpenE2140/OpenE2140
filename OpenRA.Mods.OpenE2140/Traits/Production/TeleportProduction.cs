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
[Desc("Special Production trait to use with Teleport building (WIP).")]
public class TeleportProductionInfo : AnimatedExitProductionInfo
{
	[FieldLoader.Require]
	[Desc("Position along which produced actor needs to pass before, it can move to exit cell.")]
	public readonly WVec ExitWaypointOffset = WVec.Zero;

	public override object Create(ActorInitializer init)
	{
		return new TeleportProduction(init, this);
	}
}

public class TeleportProduction : AnimatedExitProduction, ISafeDragNotify
{
	private enum CustomProductionState
	{
		None, MovingToExitWaypoint
	}

	private readonly TeleportProductionInfo info;
	private CustomProductionState customState = CustomProductionState.None;

	public TeleportProduction(ActorInitializer init, TeleportProductionInfo info)
		: base(init, info)
	{
		this.info = info;
	}

	protected override void Eject(Actor self)
	{
		if (this.productionInfo == null)
			return;

		if (this.info.ExitWaypointOffset == WVec.Zero)
		{
			base.Eject(self);
			return;
		}

		var exit = self.Location + this.productionInfo.ExitInfo.ExitCell;
		var spawnLocation = this.GetSpawnLocation(self, exit);
		var initialFacing = AnimatedExitProduction.GetInitialFacing(this.productionInfo.Producee, spawnLocation, this.GetExitWaypointOffset(self));

		var inits = this.productionInfo.Inits;
		inits.Add(new LocationInit(this.GetExitCell(self)));
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

		if (this.info.ExitWaypointOffset == WVec.Zero)
			return;

		if (!other.TryGetTrait<Mobile>(out var mobile))
			return;

		mobile.SetCenterPosition(other, self.CenterPosition + this.info.Position);

		this.State = AnimationState.Custom;
		this.customState = CustomProductionState.MovingToExitWaypoint;

		other.QueueActivity(new ProductionExitMove(other, self, this.GetExitWaypointOffset(self)));
	}

	private WPos GetExitWaypointOffset(Actor self) => self.CenterPosition + this.info.Position + this.info.ExitWaypointOffset;

	void ISafeDragNotify.SafeDragFailed(Actor self, Actor movingActor)
	{
		// TODO: should retry logic be here ???
		//this.customState = CustomProductionState.RetryMove;
	}

	void ISafeDragNotify.SafeDragComplete(Actor self, Actor movingActor)
	{
		// Produced actor might still be moving along the exit path, so don't do anything, if it does.
		if (this.customState != CustomProductionState.MovingToExitWaypoint || movingActor != this.productionInfo?.Actor)
			return;

		this.customState = CustomProductionState.None;

		// It's necessary to bypass AnimatedExitProduction here and move State and queue ProductionExitMove now.
		// The reason is that if we delay this until next tick of AnimatedExitProduction is undesirable (produced actor stops for one tick).
		this.State = AnimationState.Ejecting;

		var actor = this.productionInfo.Actor;

		var end = self.World.Map.CenterOfCell(self.Location + this.productionInfo.ExitInfo.ExitCell);
		this.productionInfo = this.productionInfo with
		{
			ExitMoveActivity = new ProductionExitMove(actor, self, end)
		};
		actor.QueueActivity(this.productionInfo.ExitMoveActivity);
	}
}
