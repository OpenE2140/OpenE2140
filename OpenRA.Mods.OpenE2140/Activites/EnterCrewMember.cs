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

using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Traits.BuildingCrew;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Activites;

public class EnterCrewMember : Enter
{
	private readonly CrewMember crewMember;
	private Actor? enterActor;
	private BuildingCrew? buildingCrew;

	public EnterCrewMember(Actor self, in Target target, Color? targetLineColor)
		: base(self, target, targetLineColor)
	{
		this.crewMember = self.Trait<CrewMember>();
	}

	protected override bool TryStartEnter(Actor self, Actor targetActor)
	{
		this.enterActor = targetActor;
		this.buildingCrew = targetActor.TraitOrDefault<BuildingCrew>();

		// Make sure we can still enter the building
		// (but not before, because this may stop the actor in the middle of nowhere)
		if (this.buildingCrew == null || this.buildingCrew.IsTraitDisabled || !this.crewMember.Reserve(self, this.buildingCrew))
		{
			this.Cancel(self, true);
			return false;
		}

		return true;
	}

	protected override void TickInner(Actor self, in Target target, bool targetIsDeadOrHiddenActor)
	{
		if (this.buildingCrew != null && this.buildingCrew.IsTraitDisabled)
			this.Cancel(self, true);
	}

	protected override void OnEnterComplete(Actor self, Actor targetActor)
	{
		self.World.AddFrameEndTask(w =>
		{
			if (self.IsDead)
				return;

			// Make sure the target hasn't changed while entering
			// OnEnterComplete is only called if targetActor is alive
			if (targetActor != this.enterActor)
				return;

			if (!this.buildingCrew.CanEnter(self))
				return;

			foreach (var inl in targetActor.TraitsImplementing<INotifyLoadCargo>())
				inl.Loading(self);

			this.buildingCrew.Load(this.enterActor, self);
			w.Remove(self);
		});
	}

	protected override void OnLastRun(Actor self)
	{
		this.crewMember.Unreserve(self);
	}

	public override void Cancel(Actor self, bool keepQueue = false)
	{
		this.crewMember.Unreserve(self);

		base.Cancel(self, keepQueue);
	}
}
