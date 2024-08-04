using OpenRA.Activities;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.OpenE2140.Traits.Resources;

namespace OpenRA.Mods.OpenE2140.Activites.Resources;

public class CrateUnload : Activity
{
	private static readonly int NonDiagonalDockDistance = 405;
	private static readonly int DiagonalDockDistance = 570;

	private enum DockingState { MovingToUnload, Drag, Dock, Loop, Undock, Complete }

	private readonly Actor self;
	private readonly Mobile mobile;
	private readonly CrateTransporter crateTransporter;
	private readonly WithSpriteBody wsb;
	private readonly CPos targetLocation;

	private DockingState dockingState;
	private CPos? unloadingCell;

	public CrateUnload(Actor self, CPos targetLocation)
	{
		this.self = self;
		this.mobile = self.Trait<Mobile>();
		this.crateTransporter = self.Trait<CrateTransporter>();
		this.wsb = self.Trait<WithSpriteBody>();
		this.targetLocation = targetLocation;
	}

	protected override void OnFirstRun(Actor self)
	{
		this.QueueChild(this.mobile.MoveTo(this.targetLocation));
		this.dockingState = DockingState.MovingToUnload;
	}

	public override bool Tick(Actor self)
	{
		switch (this.dockingState)
		{
			case DockingState.MovingToUnload:
			{
				this.unloadingCell = this.PickUnloadingCell(self);
				if (this.unloadingCell == null)
				{
					return true;
				}

				self.NotifyBlocker(this.unloadingCell.Value);

				this.QueueChild(this.mobile.MoveTo(this.unloadingCell.Value));
				this.dockingState = DockingState.Drag;

				return false;
			}
			case DockingState.Drag:
			{
				// CanUnload?
				if (this.unloadingCell == null || this.IsCanceling || !this.crateTransporter.CanUnloadAt(self, this.targetLocation))
				{
					return true;
				}

				this.crateTransporter.ReserveUnloadLocation(this.targetLocation);

				var vec = this.targetLocation - self.Location;
				var isDiagonal = vec.X != 0 && vec.Y != 0;
				var unloadPosition = self.World.Map.CenterOfCell(this.unloadingCell.Value) + new WVec(vec.X, vec.Y, 0) * (isDiagonal ? DiagonalDockDistance : NonDiagonalDockDistance);

				var ticksToDock = (self.CenterPosition - unloadPosition).Length / this.GetDockSpeed(this.unloadingCell.Value);
				if (ticksToDock > 0)
					this.QueueChild(new Drag(self, self.CenterPosition, unloadPosition, ticksToDock));

				this.dockingState = DockingState.Dock;

				return false;
			}
			case DockingState.Dock:
			{
				if (!this.IsCanceling && this.unloadingCell != null)
				{
					//dockInitiated = true;
					//PlayDockAnimations(self);
					//DockHost.OnDockStarted(DockHostActor, self, DockClient);
					//DockClient.OnDockStarted(self, DockHostActor, DockHost);
					//NotifyDocked(self);

					// TODO: play animation

					this.dockingState = DockingState.Loop;
				}
				else
				{
					this.crateTransporter.CancelUnload();
					this.dockingState = DockingState.Undock;
				}

				return false;
			}
			case DockingState.Loop:
			{
				//if (this.IsCanceling || DockHostActor.IsDead || !DockHostActor.IsInWorld || DockClient.OnDockTick(self, DockHostActor, DockHost))

				var crate = this.crateTransporter.UnloadCrate(self);
				if (crate == null)
				{
					return false;
				}

				crate!.SubActor.SetLocation(this.targetLocation);

				crate.SubActor.UnloadComplete();
				this.dockingState = DockingState.Undock;

				return false;
			}
			case DockingState.Undock:
			{
				//if (dockInitiated)
				//	PlayUndockAnimations(self);
				//else

				this.dockingState = DockingState.Complete;

				return false;
			}
			case DockingState.Complete:
			{
				// TODO: save dock position?
				this.QueueChild(new Drag(self, self.CenterPosition, self.World.Map.CenterOfCell(self.Location), 20));

				//DockHost.OnDockCompleted(DockHostActor, self, DockClient);
				//DockClient.OnDockCompleted(self, DockHostActor, DockHost);
				//NotifyUndocked(self);
				//if (IsDragRequired)
				//	this.QueueChild(new Drag(self, EndDrag, StartDrag, DragLength));
				return true;
			}
		}

		throw new InvalidOperationException("Invalid crate transporter unload state");
	}

	private int GetDockSpeed(CPos cell)
	{
		var speedModifier = 30;
		return this.mobile.Locomotor.MovementSpeedForCell(cell) * speedModifier / 100;
	}

	private CPos? PickUnloadingCell(Actor self)
	{
		// TODO: prefer cell that won't require the transporter to turn
		var candidateCells = Util.ExpandFootprint(self.Location, true).Exclude(self.Location);
		return candidateCells
			.Cast<CPos?>()
			.Where(p => this.mobile.CanEnterCell(p!.Value, self, BlockedByActor.All))
			.RandomOrDefault(self.World.SharedRandom);
	}
}
