using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Activites;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Primitives;

namespace OpenRA.Mods.OpenE2140.Traits.Resources.Activities;

public class MobileCrateUnload : CrateUnloadBase
{
	private readonly Mobile mobile;
	private readonly MobileCrateTransporter mobileCrateTransporter;
	private readonly MoveCooldownHelper moveCooldownHelper;

	private CPos? unloadingCell;

	public MobileCrateUnload(Actor self, CPos? targetLocation = null)
		: base(self, targetLocation)
	{
		this.mobile = self.Trait<Mobile>();
		this.mobileCrateTransporter = self.Trait<MobileCrateTransporter>();

		this.moveCooldownHelper = new MoveCooldownHelper(self.World, this.mobile);
	}

	protected override void InitialMoveToCrate(Actor self, CPos targetLocation)
	{
		this.moveCooldownHelper.NotifyMoveQueued();
		this.QueueChild(this.mobile.MoveTo(targetLocation, ignoreActor: self, targetLineColor: Color.Green));
	}

	protected override bool CanUnloadCrateNow(Actor self, CPos targetLocation)
	{
		return self.Location == this.unloadingCell && (targetLocation - self.Location).Length == 1;
	}

	protected override void StartDocking(Actor self, Action continuationCallback)
	{
		this.QueueChild(new ResourceCrateMovementActivity(self, false, DockAnimation.Docking, this.mobileCrateTransporter.Info.UnloadSequence, continuationCallback));
	}

	protected override void StartUndocking(Actor self, Action continuationCallback)
	{
		this.QueueChild(new ResourceCrateMovementActivity(self, false, DockAnimation.Undocking, this.mobileCrateTransporter.Info.UnloadSequence, continuationCallback));
	}

	protected override bool TryGetDockToDockPosition(Actor self, CPos targetLocation)
	{
		if (this.moveCooldownHelper.TryTick(false, out var result))
			return result.Value;

		if (this.unloadingCell == null)
		{
			this.unloadingCell = this.PickUnloadingCell(self);
			if (this.unloadingCell == null)
				return true;
		}

		if (!this.CanUnloadCrateNow(self, targetLocation))
		{
			this.moveCooldownHelper.NotifyMoveQueued();
			this.QueueChild(this.mobile.MoveTo(this.unloadingCell.Value, ignoreActor: self));

			return false;
		}

		var desiredFacing = (self.Location - targetLocation).ToWVec().Yaw;

		if (this.mobile.Facing != desiredFacing)
		{
			this.QueueChild(new Turn(self, desiredFacing));
			return false;
		}

		return true;
	}

	protected override void StartDragging(Actor self, CPos targetLocation)
	{
		// TODO: check if the unload location is still available, ReserveUnloadLocation -> bool TryReserveUnloadLocation()
		this.mobileCrateTransporter.ReserveUnloadLocation(targetLocation);

		var vec = targetLocation - self.Location;
		var loadPosition = self.World.Map.CenterOfCell(self.Location) + CrateLoadUnloadHelpers.GetDockVector(vec);

		this.DragToPosition(self, loadPosition, self.Location);
	}

	protected override void StartUndragging(Actor self)
	{
		this.DragToPosition(self, self.World.Map.CenterOfCell(self.Location), self.Location);
	}

	private void DragToPosition(Actor self, WPos targetPosition, CPos cell)
	{
		this.TryQueueChild(CommonActivities.DragToPosition(self, this.mobile, targetPosition, cell, this.mobileCrateTransporter.Info.DockSpeedModifier));
	}

	private CPos? PickUnloadingCell(Actor self)
	{
		return Util.ExpandFootprint(self.Location, true).Exclude(self.Location)
			.Where(c => this.mobile.CanStayInCell(c) && this.mobile.CanEnterCell(c, self, BlockedByActor.All))
			.OrderBy(c =>
			{
				// Order candidate cells by the angle to target cell (i.e. cell on which CrateTransporter currently is)
				// This will make CrateTransporter pick unloading cell that will take least amount of time to move onto.
				var turnAngle = (c - self.Location).ToWVec().Yaw;
				return new WAngle((turnAngle.Angle - self.Orientation.Yaw.Angle) * Util.GetTurnDirection(self.Orientation.Yaw, turnAngle)).Angle;
			})
			.Cast<CPos?>()
			.FirstOrDefault();
	}

}
