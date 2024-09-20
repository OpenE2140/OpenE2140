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
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Primitives;

namespace OpenRA.Mods.OpenE2140.Traits.Resources.Activities;

public class AircraftCrateUnload : CrateUnloadBase
{
	private readonly AircraftCrateTransporterInfo info;
	private readonly Aircraft aircraft;
	private readonly WithSpriteBody wsb;
	private readonly int liftSequenceFacings;
	private readonly int idleSequenceFacings;

	public AircraftCrateUnload(Actor self, CPos? targetLocation, AircraftCrateTransporterInfo info)
		: base(self, targetLocation)
	{
		this.info = info;

		this.aircraft = self.Trait<Aircraft>();
		this.wsb = self.Trait<WithSpriteBody>();
		this.liftSequenceFacings = this.wsb.DefaultAnimation.GetSequence(this.info.DockSequence).Facings;
		this.idleSequenceFacings = this.wsb.DefaultAnimation.GetSequence(this.wsb.Info.Sequence).Facings;
	}

	protected override void InitialMoveToCrate(Actor self, CPos targetLocation)
	{
		if (this.CanUnloadCrateNow(self, targetLocation))
		{
			return;
		}

		this.QueueChild(this.aircraft.MoveTo(targetLocation, targetLineColor: Color.Green));
	}

	protected override void StartDocking(Actor self, Action continuationCallback)
	{
		this.wsb.DefaultAnimation.Play(this.info.DockSequence);
		continuationCallback();
	}

	protected override void StartUndocking(Actor self, Action continuationCallback)
	{
		// TODO
		continuationCallback();
	}

	protected override bool CanUnloadCrateNow(Actor self, CPos targetLocation)
	{
		var cellCenter = self.World.Map.CenterOfCell(targetLocation);
		return (targetLocation - self.Location).Length == 0
			&& self.CenterPosition.EqualsHorizontally(cellCenter)
			&& self.World.Map.DistanceAboveTerrain(this.aircraft.CenterPosition) >= this.info.LandAltitude;
	}

	protected override bool TryGetDockToDockPosition(Actor self, CPos targetLocation)
	{
		if (!this.CanUnloadCrateNow(self, targetLocation))
		{
			this.InitialMoveToCrate(self, targetLocation);

			return false;
		}

		return true;
	}

	protected override void StartDragging(Actor self, CPos targetLocation)
	{
		var turnAngle = this.aircraft.Facing;

		// First, pick closest allowed angle
		var desiredFacing = AircraftCrateTransporter.GetDockAngle(self.Orientation, this.info.AllowedDockAngles, this.liftSequenceFacings, this.idleSequenceFacings);

		// Then flip the facing, if will less amount of time for Heavy Lifter to turn to
		if ((this.aircraft.Facing - desiredFacing).Angle > 256 && (desiredFacing - this.aircraft.Facing).Angle > 256)
			desiredFacing = new WAngle(desiredFacing.Angle + 512);

		this.QueueChild(new LandOnGround(this.aircraft, () => desiredFacing, this.info.LandAltitude));
	}

	protected override void StartUndragging(Actor self)
	{
		if (this.IsCanceling)
			this.QueueChild(new TakeOff(self));
		else
			this.QueueChild(new TimedAnimation(this.wsb, this.info.DockSequence, 10).WithChild(new TakeOff(self)));
	}

	public class LandOnGround : Activity
	{
		private readonly Aircraft aircraft;
		private readonly Func<WAngle> angle;
		private readonly WDist altitude;

		public LandOnGround(Aircraft aircraft, Func<WAngle> angle, WDist altitude)
		{
			this.aircraft = aircraft;
			this.angle = angle;
			this.altitude = altitude;
		}

		protected override void OnFirstRun(Actor self)
		{
			var dat = self.World.Map.DistanceAboveTerrain(this.aircraft.CenterPosition);

			if (dat <= this.altitude)
			{
				var desiredFacing = this.angle();
				var facing = Util.TickFacing(self.Orientation.Yaw, desiredFacing, this.aircraft.Info.TurnSpeed);
				if (facing == desiredFacing)
				{
					this.aircraft.Facing = desiredFacing;
				}
				else
				{
					this.QueueChild(new TakeOff(self));
				}
			}
			else
			{
				this.aircraft.AddInfluence(self.Location);
				this.aircraft.EnteringCell(self);
			}
		}

		public override bool Tick(Actor self)
		{
			var dat = self.World.Map.DistanceAboveTerrain(this.aircraft.CenterPosition);

			if (this.IsCanceling)
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

	private class TimedAnimation : Activity
	{
		private readonly WithSpriteBody wsb;
		private readonly string sequence;

		private int animationTicks;

		public TimedAnimation(WithSpriteBody wsb, string sequence, int animationTicks)
		{
			this.ChildHasPriority = false;
			this.wsb = wsb;
			this.sequence = sequence;
			this.animationTicks = animationTicks;
		}

		protected override void OnFirstRun(Actor self)
		{
			this.wsb.DefaultAnimation.Play(this.sequence);
		}

		public override bool Tick(Actor self)
		{
			if (--this.animationTicks <= 0)
			{
				this.wsb.CancelCustomAnimation(self);
			}

			return this.TickChild(self);
		}
	}
}
