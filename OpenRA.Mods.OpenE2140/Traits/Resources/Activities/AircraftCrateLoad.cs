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

using OpenRA.Activities;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Resources.Activities;

public class AircraftCrateLoad : CrateLoadBase
{
	private readonly Aircraft aircraft;
	private readonly AircraftCrateTransporterInfo info;

	public AircraftCrateLoad(Actor self, in Target crateActor, AircraftCrateTransporterInfo info)
		: base(self, crateActor)
	{
		this.aircraft = self.Trait<Aircraft>();
		this.info = info;
	}

	protected override void InitialMoveToCrate(Actor self, Target target)
	{
		this.QueueChild(this.aircraft.MoveToTarget(self, target, targetLineColor: Color.Green));
	}

	protected override void StartDocking(Actor self, Action continuationCallback)
	{
		// TODO
		continuationCallback();
	}

	protected override void StartUndocking(Actor self, Action continuationCallback)
	{
		// TODO
		continuationCallback();
	}

	protected override bool CanLoadCrateNow(Actor self, Target target)
	{
		return target.Type == TargetType.Actor
			&& (target.Actor.Location - self.Location).Length == 0
			&& self.World.Map.DistanceAboveTerrain(this.aircraft.CenterPosition) >= this.info.LandAltitude;
	}

	protected override bool TryGetDockToDockPosition(Actor self, Target target, bool targetIsHiddenActor)
	{
		if (!this.CanLoadCrateNow(self, target))
		{
			this.InitialMoveToCrate(self, target);

			return false;
		}

		return true;
	}

	protected override void StartDragging(Actor self, Target target)
	{
		var turnAngle = this.aircraft.Facing;

		var desiredFacing = this.info.AllowedDockAngles.FirstOrDefault(a => a.Angle >= turnAngle.Angle);
		desiredFacing = target.Actor.Trait<IFacing>().Facing;
		// TODO: pick facing, which will take least amount of time for Heavy Lifter to turn to.
		if ((this.aircraft.Facing - desiredFacing).Angle > 512)
			desiredFacing = new WAngle(desiredFacing.Angle - 1024);

		this.QueueChild(new LandOnCrate(this.aircraft, target, () => desiredFacing, this.info.LandAltitude));
	}

	protected override void StartUndragging(Actor self)
	{
		if (this.NextActivity != null)
			this.QueueChild(new TakeOff(self));
	}

	public class LandOnCrate : Activity
	{
		private readonly Aircraft aircraft;
		private readonly Target target;
		private readonly Func<WAngle> angle;
		private readonly WDist altitude;

		public LandOnCrate(Aircraft aircraft, in Target target, Func<WAngle> angle, WDist altitude)
		{
			this.aircraft = aircraft;
			this.target = target;
			this.angle = angle;
			this.altitude = altitude;
		}

		protected override void OnFirstRun(Actor self)
		{
			var dat = self.World.Map.DistanceAboveTerrain(this.aircraft.CenterPosition);
			if (dat > this.altitude)
			{
				this.aircraft.AddInfluence(self.Location);
				this.aircraft.EnteringCell(self);
			}
		}

		public override bool Tick(Actor self)
		{
			var dat = self.World.Map.DistanceAboveTerrain(this.aircraft.CenterPosition);

			if (this.IsCanceling || this.target.Type == TargetType.Invalid || !this.target.Actor.IsInWorld)
			{
				if (dat > this.aircraft.LandAltitude && dat < this.aircraft.Info.CruiseAltitude)
				{
					this.QueueChild(new TakeOff(self));
					return false;
				}

				this.aircraft.RemoveInfluence();
				return true;
			}

			if (dat > this.altitude)
			{
				Fly.VerticalTakeOffOrLandTick(self, this.aircraft, this.angle(), this.altitude);
				return false;
			}

			return true;
		}
	}
}
