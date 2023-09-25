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
	private readonly bool exitAll;
	private readonly bool playNotification;
    private readonly BuildingCrew buildingCrew;
    private readonly INotifyBuildingCrewExit[] notifiers;

	public CrewExit(Actor self, bool exitAll = true, bool playNotification = true)
	{
		this.self = self;
        this.exitAll = exitAll;
        this.playNotification = playNotification;
		this.buildingCrew = self.Trait<BuildingCrew>();
		this.notifiers = self.TraitsImplementing<INotifyBuildingCrewExit>().ToArray();
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
		if (this.playNotification)
			Game.Sound.PlayNotification(self.World.Map.Rules, self.Owner, "Speech", this.buildingCrew.Info.ExitBuildingNotification, self.Owner.Faction.InternalName);

        this.QueueChild(new Wait(this.buildingCrew.Info.BeforeExitDelay));
    }

    public override bool Tick(Actor self)
    {
        if (this.IsCanceling || this.buildingCrew.IsEmpty())
            return true;

        if (this.buildingCrew.CanExit())
        {
            foreach (var inbce in this.notifiers)
                inbce.Exiting(self);

            var actor = this.buildingCrew.Peek();
            var spawn = self.CenterPosition;

            var exitSubCell = this.ChooseExitSubCell(actor);
            if (exitSubCell == null)
            {
                self.NotifyBlocker(this.BlockedExitCells(actor));
                this.QueueChild(new Wait(10));
                return false;
            }

            this.buildingCrew.Exit(self);
            self.World.AddFrameEndTask(w =>
            {
                if (actor.Disposed)
                    return;

                var move = actor.Trait<IMove>();
                var pos = actor.Trait<IPositionable>();
                var crewMember = actor.Trait<CrewMember>();

                pos.SetPosition(actor, exitSubCell.Value.Cell, exitSubCell.Value.SubCell);
                pos.SetCenterPosition(actor, spawn);

                if (crewMember.Info.CancelActivitiesOnExit)
                    actor.CancelActivity();
                w.Add(actor);
            });
        }

        if (!this.exitAll || !this.buildingCrew.CanExit())
        {
            if (this.buildingCrew.Info.AfterExitDelay > 0)
                this.QueueChild(new Wait(this.buildingCrew.Info.AfterExitDelay, false));

            return true;
        }

        return false;
    }
}
