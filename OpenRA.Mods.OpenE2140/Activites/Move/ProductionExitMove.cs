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
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Mods.OpenE2140.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Activites.Move;

public class ProductionExitMove : Activity
{
	private enum ExitMoveState { None, Waiting, Dragging }

	private readonly Mobile mobile;
	private readonly ISafeDragNotify[] safeDragNotify;
	private readonly Actor producent;
	private readonly WPos start;
	private readonly WPos? end;
	private readonly int maxAttempts;
	private readonly CPos targetCell;

	private ExitMoveState state = ExitMoveState.None;
	private int waitTicks;
	private int attempt;

	public ProductionExitMove(Actor self, Actor producent, WPos end, int maxAttempts = 3)
		: this(self, producent, maxAttempts)
	{
		this.end = end;
		this.targetCell = self.World.Map.CellContaining(end);
	}

	public ProductionExitMove(Actor self, Actor producent, CPos targetCell, int maxAttempts = 3)
		: this(self, producent, maxAttempts)
	{
		this.targetCell = targetCell;
	}

	private ProductionExitMove(Actor self, Actor producent, int maxAttempts)
	{
		this.mobile = self.Trait<Mobile>();
		this.start = self.CenterPosition;
		this.safeDragNotify = producent.TryGetTraitsImplementing<ISafeDragNotify>().ToArray();
		this.producent = producent;
		this.IsInterruptible = false;
		this.maxAttempts = maxAttempts;
	}

	private Drag CreateDrag(Actor self, WPos end)
	{
		var speed = this.mobile.MovementSpeedForCell(self.World.Map.CellContaining(end));
		var length = speed > 0 ? (end - this.start).Length / speed : 0;

		var delta = end - this.start;
		var facing = delta.HorizontalLengthSquared != 0 ? delta.Yaw : this.mobile.Facing;
		return new Drag(self, this.start, end, length, facing);
	}

	public override bool Tick(Actor self)
	{
		switch (this.state)
		{
			case ExitMoveState.None:
			{
				// Determine precise position and subcell where the actor should be moved.
				var subCell = SubCell.FullCell;
				var endPos = this.end;
				var targetCellCenter = self.World.Map.CenterOfCell(this.targetCell);

				// Check if actor can share cell, if so we need to determine subcell it should move into
				if (this.mobile.Info.LocomotorInfo.SharesCell)
				{
					if (endPos == null)
						// End position was not specified, pick any.
						subCell = SubCell.Any;
					else
						// End position was specified, approximates the subcell from it.
						subCell = FindApproximateSubCell(self, endPos.Value - targetCellCenter);

					// Get free subcell from target cell taking into account approximated subcell (if any).
					subCell = this.mobile.GetAvailableSubCell(this.targetCell, subCell, self);
					subCell = this.mobile.GetValidSubCell(subCell == SubCell.Invalid ? SubCell.Any : subCell);
					endPos = targetCellCenter + self.World.Map.Grid.OffsetOfSubCell(subCell);
				}
				else if (endPos == null)
					endPos = targetCellCenter;

				var blockingActors = self.World.ActorMap.GetActorsAt(this.targetCell, subCell)
					.Where(a => a != self && a != this.producent)
					.ToArray();
				if (!blockingActors.Any())
				{
					// Reserve the exit cell
					this.mobile.SetPosition(self, this.targetCell, subCell);
					this.mobile.SetCenterPosition(self, this.start);

					this.state = ExitMoveState.Dragging;
					this.QueueChild(this.CreateDrag(self, endPos.Value));
				}
				else
				{
					this.attempt++;
					this.state = ExitMoveState.Waiting;
					this.waitTicks = 3;
				}
				break;
			}
			case ExitMoveState.Waiting:
			{
				if (this.waitTicks-- >= 0)
					break;

				this.state = ExitMoveState.None;
				if (this.attempt >= this.maxAttempts)
				{
					this.NotifyDragFailed(self);
					return true;
				}

				break;
			}
			case ExitMoveState.Dragging:
			{
				this.NotifyDragComplete(self);
				return true;
			}
			default:
				throw new ArgumentOutOfRangeException($"Unknown state: {this.state}");
		}

		return false;
	}

	/// <summary>
	/// Finds nearest subcell to <paramref name="cellOffset"/>.
	/// </summary>
	private static SubCell FindApproximateSubCell(Actor self, WVec cellOffset)
	{
		return self.World.Map.Grid.SubCellOffsets
			.Skip(1)
			.Select((offset, i) => (Offset: offset, SubCell : (SubCell)i + 1))
			.MinBy(t => (t.Offset - cellOffset).HorizontalLengthSquared).SubCell;
	}

	private void NotifyDragFailed(Actor self)
	{
		Array.ForEach(this.safeDragNotify, t => t.SafeDragFailed(this.producent, self));
	}

	private void NotifyDragComplete(Actor self)
	{
		Array.ForEach(this.safeDragNotify, t => t.SafeDragComplete(this.producent, self));
	}
}
