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
using OpenRA.Mods.OpenE2140.Traits.Resources.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

public class AircraftCrateTransporterInfo : CrateTransporterInfo
{
	[Desc("List of angles, at which the aircraft crate transporter can land/dock.")]
	public readonly WAngle[] AllowedDockAngles = [new(0)];

	[Desc("Altitude at which the aircraft considers itself landed with a resource crate loaded.")]
	public readonly WDist LandAltitude = WDist.Zero;

	public override object Create(ActorInitializer init)
	{
		return new AircraftCrateTransporter(init, this);
	}
}

public class AircraftCrateTransporter : CrateTransporter, IAircraftCenterPositionOffset
{
	private readonly Actor self;
	private readonly Aircraft aircraft;
	private readonly BodyOrientation body;
	private readonly WithSpriteBody wsb;

	public new AircraftCrateTransporterInfo Info;

	public AircraftCrateTransporter(ActorInitializer init, AircraftCrateTransporterInfo info)
		: base(init, info)
	{
		this.Info = info;

		this.self = init.Self;
		this.aircraft = init.Self.Trait<Aircraft>();
		this.body = init.Self.Trait<BodyOrientation>();
		this.wsb = init.Self.Trait<WithSpriteBody>();
	}

	WVec IAircraftCenterPositionOffset.PositionOffset
	{
		get
		{
			if (!this.HasCrate) return WVec.Zero;

			var localOffset = new WVec(0, 0, -this.Info.LandAltitude.Length).Rotate(this.body.QuantizeOrientation(this.self.Orientation));
			return this.body.LocalToWorld(localOffset);
		}
	}

	protected override Activity GetCrateUnloadActivity(Actor self, Order order)
	{
		CPos? targetLocation = order.Target.Type != TargetType.Invalid ? self.World.Map.CellContaining(order.Target.CenterPosition) : null;
		return new AircraftCrateUnload(self, targetLocation, this.Info);
	}

	protected override Activity GetCrateLoadActivity(Actor self, Order order)
	{
		return new AircraftCrateLoad(self, order.Target, this.Info);
	}

	public static WAngle GetDockAngle(WRot orientation, WAngle[] allowedDockAngles, int newSequenceFacings, int? originalSequenceFacings)
	{
		// TODO: this does not work properly, if sequence facings don't match allowed dock angles
		var angles = allowedDockAngles
			.OrderBy(x =>
			{
				// Quantizing to 8 facings directly from original Aircraft's orientation can sometimes pick facing in the opposite direction
				// than the facing quantized to 16 facings.
				// Quantizing the facing twice (first to 16 facings and then to 8 facings) solves some of the cases.
				// However this formula can still sometimes can produce incorrect facing:
				// - Heavy Lifter's facing changes in the same tick as the GetDockAngle() is called
				// - the quantized facing is changed (i.e. sprite needs to change), but the new sprite has not been rendered yet
				// - the dock angle is however calculated from the new angle
				// - this is caused by how the Fly activity changes Aircraft's position and facing

				var f0 = Util.QuantizeFacing(orientation.Yaw, originalSequenceFacings != null ? originalSequenceFacings.Value : newSequenceFacings);
				var f1 = Util.QuantizeFacing(f0, newSequenceFacings);

				return new WAngle((x.Angle - f1.Angle) * Util.GetTurnDirection(orientation.Yaw, x)).Angle;
			}).ToArray();
		return angles.FirstOrDefault();
	}

	/// <summary>
	/// Calculates delay for lift animation for specified altitude. Uses <see cref="AircraftInfo.AltitudeVelocity"/> to properly calculate the delay.
	/// </summary>
	/// <param name="landAltitude">Altitude at which the <see cref="AircraftCrateTransporter"/> is going to land.</param>
	/// <returns>Delay in ticks, after which the lift animation should start.</returns>
	public int GetLiftAnimationStartDelay(int landAltitude)
	{
		var minAltitudeForAnimationStart = 768;

		var dat = this.self.World.Map.DistanceAboveTerrain(this.aircraft.CenterPosition);
		if (dat.Length <= landAltitude)
			return 0;

		var diff = dat.Length - landAltitude;

		// The animation should start playing at approximately half of the distance between current Aircraft's altitude and land altitude.
		// However, we need to give enough time for the crate transporter to be able to "open its hooks" for the crate,
		// so we define minimum altitude at which the animation needs to start playing.
		return Math.Max(diff - minAltitudeForAnimationStart, 0) / this.aircraft.Info.AltitudeVelocity.Length;
	}
}
