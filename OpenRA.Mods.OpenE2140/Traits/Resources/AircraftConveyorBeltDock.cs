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
using OpenRA.Graphics;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Mods.OpenE2140.Traits.Resources.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

public class AircraftConveyorBeltDockInfo : SharedDockHostInfo
{
	[Desc(
		"List of angles, which the aircraft crate transporter can dock into this dock host.",
		$"All angles must be also present in {nameof(AircraftCrateTransporter)}.{nameof(AircraftCrateTransporterInfo.AllowedDockAngles)}.",
		$"In other words, angles in {nameof(AircraftConveyorBeltDock)} are subset of angles in {nameof(AircraftCrateTransporter)}")]
	public readonly WAngle[] AllowedDockAngles = [new(0)];

	[Desc($"Altitude at which the aircraft considers itself landed with on top of the {nameof(AircraftConveyorBeltDockInfo)}.")]
	public readonly WDist LandAltitude = WDist.Zero;

	public override object Create(ActorInitializer init)
	{
		return new AircraftConveyorBeltDock(init.Self, this);
	}
}

public class AircraftConveyorBeltDock : SharedDockHost, IConveyorBeltDockHost
{
	public new readonly AircraftConveyorBeltDockInfo Info;

	public AircraftConveyorBeltDock(Actor self, AircraftConveyorBeltDockInfo info)
		: base(self, info)
	{
		this.Info = info;
	}

	public override bool QueueMoveActivity(Activity moveToDockActivity, Actor self, Actor clientActor, DockClientManager client, MoveCooldownHelper moveCooldownHelper)
	{
		var aircraft = clientActor.Trait<Aircraft>();

		// Make sure the actor is at the dock and at correct facing.
		if (!clientActor.CenterPosition.EqualsHorizontally(this.DockPosition)
			|| this.Info.AllowedDockAngles.IndexOf(aircraft.Facing) == -1
			|| aircraft.GetPosition().Z != this.Info.LandAltitude.Length)
		{
			moveCooldownHelper.NotifyMoveQueued();

			moveToDockActivity.QueueChild(new AircraftMoveToConveyorBelt(clientActor, self, this, self.Info.HasTraitInfo<ResourceMineInfo>()));
			return true;
		}

		return false;
	}

	public override void QueueDockActivity(Activity moveToDockActivity, Actor self, Actor clientActor, DockClientManager client)
	{
		var dockActivity = new GenericDockSequence(
			clientActor,
			client,
			self,
			this,
			this.Info.DockWait,
			this.Info.IsDragRequired,
			this.Info.DragOffset,
			this.Info.DragLength);

		moveToDockActivity.QueueChild(new ReleaseDockHostLock(this, dockActivity));
	}

	Activity IConveyorBeltDockHost.GetInnerDockActivity(Actor self, Actor clientActor, Action continuationCallback, ConveyorBeltInnerDockContext context)
	{
		var crateTransporter = clientActor.Trait<CrateTransporter>();
		if (context.Animation == DockAnimation.Docking)
			return new PlayAnimation(clientActor, crateTransporter.Info.DockSequence, continuationCallback);
		else
		{
			// Let undocking continue immediately
			continuationCallback();

			var wsb = clientActor.Trait<WithSpriteBody>();

			var waitBeforeTakeOff = new Wait(5);

			if (context.IsLoading)
			{
				// When loading a resource crate, the lift off animation needs to end immediately
				wsb.CancelCustomAnimation(clientActor);

				waitBeforeTakeOff.Queue(new TakeOff(clientActor));
			}
			else
			{
				// When unloading a resource crate, the lift off animation can play for a bit longer
				// A few ticks is enough, there's no need to do any precise calculation for altitude, velocity, etc.
				waitBeforeTakeOff.Queue(new DelayCancelAnimation(wsb, delay: 10).WithChild(new TakeOff(clientActor)));
			}

			return waitBeforeTakeOff;
		}
	}

	private class AircraftMoveToConveyorBelt : Activity
	{
		private readonly Aircraft aircraft;
		private readonly Actor dockHostActor;
		private readonly AircraftConveyorBeltDock aircraftConveyorBeltDock;
		private readonly bool isLoading;
		private readonly AircraftCrateTransporter crateTransporter;

		private WPos DockPosition => this.aircraftConveyorBeltDock.DockPosition;

		private WDist LandAltitude => this.aircraftConveyorBeltDock.Info.LandAltitude;

		private readonly WAngle[] allowedDockAngles;
		private readonly WithSpriteBody wsb;
		private readonly ISpriteSequence dockSequence;
		private readonly ISpriteSequence idleSequence;

		public AircraftMoveToConveyorBelt(Actor self, Actor dockHostActor, AircraftConveyorBeltDock aircraftConveyorBeltDock, bool isLoading)
		{
			this.aircraft = self.Trait<Aircraft>();
			this.dockHostActor = dockHostActor;
			this.aircraftConveyorBeltDock = aircraftConveyorBeltDock;
			this.isLoading = isLoading;

			this.crateTransporter = self.Trait<AircraftCrateTransporter>();

			// Allowed dock angles are restricted by the angles allowed by conveyor belt dock and the crate transporter itself
			this.allowedDockAngles = this.aircraftConveyorBeltDock.Info.AllowedDockAngles.Intersect(this.crateTransporter.Info.AllowedDockAngles).ToArray();

			this.wsb = self.Trait<WithSpriteBody>();

			// In order to properly calculate docking angle, we need to know how many facings the dock sequence has
			this.dockSequence = this.wsb.DefaultAnimation.GetSequence(this.crateTransporter.Info.DockSequence);
			this.idleSequence = this.wsb.DefaultAnimation.GetSequence(this.wsb.Info.Sequence);
		}

		protected override void OnFirstRun(Actor self)
		{
			this.QueueChild(this.aircraft.MoveToTarget(self, Target.FromPos(this.DockPosition), null, null));

			var landOnCrate = new AircraftCrateLoad.LandOnCrate(
				this.aircraft, Target.FromActor(this.dockHostActor), GetDockAngle,
				this.LandAltitude);

			// Parent activity is the activity that represents the entire docking process (including any animation)
			Activity parentActivity;
			if (this.isLoading)
			{
				// When crate is being loaded, start playing lift animation at correct time (see GetLiftAnimationStartDelay() for more details)
				var animation = new LoadUnloadAnimation(this.wsb, this.dockSequence.Name,
					shouldStartPlaying: () => this.aircraft.Facing == GetDockAngle(),
					() => this.crateTransporter.GetLiftAnimationStartDelay(this.LandAltitude.Length));
				animation.QueueChild(landOnCrate);

				parentActivity = animation;
			}
			else
			{
				// When crate is being unloaded, the lift animation cannot be played while the crate transporter is landing on the conveyor belt
				// (the crate is still attached). Thus no animation activity is wrapped around the landing activity.
				parentActivity = landOnCrate;
			}

			// Acquire lock now (i.e. before the landing starts) and keep it until the docking is complete
			this.QueueChild(new DockHostLock(this.aircraftConveyorBeltDock, parentActivity, releaseOnFinish: false));

			// Dock angle is calculated lazily (at the very last moment, just before the landing starts).
			// The calculation needs some work (see GetDockAngle() method in AircraftCrateTransporter
			WAngle GetDockAngle() => AircraftCrateTransporter.GetDockAngle(
				self.Orientation,
				this.allowedDockAngles,
				this.dockSequence.Facings,
				this.idleSequence.Facings);
		}
	}

	private class LoadUnloadAnimation : Activity
	{
		private readonly WithSpriteBody wsb;
		private readonly string sequence;
		private readonly Func<bool> shouldStartPlaying;
		private readonly Func<int> delay;

		private int tickDelay;

		public LoadUnloadAnimation(WithSpriteBody wsb, string sequence, Func<bool> shouldStartPlaying, Func<int> delay)
		{
			this.ChildHasPriority = false;
			this.wsb = wsb;
			this.sequence = sequence;
			this.shouldStartPlaying = shouldStartPlaying;
			this.delay = delay;
		}

		protected override void OnFirstRun(Actor self)
		{
			this.tickDelay = this.delay();
		}

		public override bool Tick(Actor self)
		{
			if (this.IsCanceling || this.ChildActivity?.IsCanceling == true)
			{
				if (this.wsb.DefaultAnimation.IsPlayingSequence(this.sequence))
					this.wsb.CancelCustomAnimation(self);
			}
			else
			{
				if (--this.tickDelay <= 0 && this.shouldStartPlaying() && !this.wsb.DefaultAnimation.IsPlayingSequence(this.sequence))
				{
					this.wsb.CancelCustomAnimation(self);
					this.wsb.PlayCustomAnimationRepeating(self, this.sequence);
				}
			}

			return this.TickChild(self);
		}
	}
}
