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
using OpenRA.Mods.OpenE2140.Traits.BuildingCrew;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Activites;

public class CrewExit : Activity
{
	private readonly Actor self;
	private readonly BuildingCrew buildingCrew;
	private readonly INotifyUnloadCargo[] notifiers;
	private readonly bool unloadAll;
	private readonly Mobile mobile;
	private readonly bool assignTargetOnFirstRun;
	private readonly WDist unloadRange;
	private Target destination;

	public CrewExit(Actor self, WDist unloadRange, bool unloadAll = true)
		: this(self, Target.Invalid, unloadRange, unloadAll)
	{
		this.assignTargetOnFirstRun = true;
	}

	public CrewExit(Actor self, in Target destination, WDist unloadRange, bool unloadAll = true)
	{
		this.self = self;
		this.buildingCrew = self.Trait<BuildingCrew>();
		this.notifiers = self.TraitsImplementing<INotifyUnloadCargo>().ToArray();
		this.unloadAll = unloadAll;
		this.mobile = self.TraitOrDefault<Mobile>();
		this.destination = destination;
		this.unloadRange = unloadRange;
	}

	public (CPos Cell, SubCell SubCell)? ChooseExitSubCell(Actor crewMember)
	{
		var pos = crewMember.Trait<IPositionable>();

		return this.buildingCrew.CurrentAdjacentCells
			.Shuffle(this.self.World.SharedRandom)
			.Select(c => (c, pos.GetAvailableSubCell(c)))
			.Cast<(CPos, SubCell SubCell)>()
			.FirstOrDefault(s => s.SubCell != SubCell.Invalid);
	}

	private IEnumerable<CPos> BlockedExitCells(Actor crewMember)
	{
		var pos = crewMember.Trait<IPositionable>();

		// Find the cells that are blocked by transient actors
		return this.buildingCrew.CurrentAdjacentCells
			.Where(c => pos.CanEnterCell(c, null, BlockedByActor.All) != pos.CanEnterCell(c, null, BlockedByActor.None));
	}

	protected override void OnFirstRun(Actor self)
	{
		if (this.assignTargetOnFirstRun)
			this.destination = Target.FromCell(self.World, self.Location);

		// Move to the target destination
		if (this.mobile != null)
		{
			var cell = self.World.Map.Clamp(this.self.World.Map.CellContaining(this.destination.CenterPosition));
			this.QueueChild(new Common.Activities.Move(self, cell, this.unloadRange));
		}

		this.QueueChild(new Wait(this.buildingCrew.Info.BeforeUnloadDelay));
	}

	public override bool Tick(Actor self)
	{
		if (this.IsCanceling || this.buildingCrew.IsEmpty())
			return true;

		if (this.buildingCrew.CanUnload())
		{
			foreach (var inu in this.notifiers)
				inu.Unloading(self);

			var actor = this.buildingCrew.Peek();
			var spawn = self.CenterPosition;

			var exitSubCell = this.ChooseExitSubCell(actor);
			if (exitSubCell == null)
			{
				self.NotifyBlocker(this.BlockedExitCells(actor));
				this.QueueChild(new Wait(10));
				return false;
			}

			this.buildingCrew.Unload(self);
			self.World.AddFrameEndTask(w =>
			{
				if (actor.Disposed)
					return;

				var move = actor.Trait<IMove>();
				var pos = actor.Trait<IPositionable>();

				pos.SetPosition(actor, exitSubCell.Value.Cell, exitSubCell.Value.SubCell);
				pos.SetCenterPosition(actor, spawn);

				actor.CancelActivity();
				w.Add(actor);
			});
		}

		if (!this.unloadAll || !this.buildingCrew.CanUnload())
		{
			if (this.buildingCrew.Info.AfterUnloadDelay > 0)
				this.QueueChild(new Wait(this.buildingCrew.Info.AfterUnloadDelay, false));

			return true;
		}

		return false;
	}
}
