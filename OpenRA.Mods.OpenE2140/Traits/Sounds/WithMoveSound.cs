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

using JetBrains.Annotations;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Sounds;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("Makes an actor play a sound while moving.")]
public class WithMoveSoundInfo : TraitInfo, IRulesetLoaded
{
	[FieldLoader.Require]
	public readonly string Sound = "";

	public override object Create(ActorInitializer init)
	{
		return new WithMoveSound(this);
	}

	public void RulesetLoaded(Ruleset rules, ActorInfo info)
	{
		var isMobile = info.HasTraitInfo<MobileInfo>();
		var isAircraft = info.HasTraitInfo<AircraftInfo>();

		if (!isMobile && !isAircraft)
		{
			throw new YamlException(
				nameof(WithMoveSound)
				+ " trait requires actor to have either Mobile or Aircraft trait (not both at the same time). "
				+ $"Actor '{info.Name}' does not satisfy this requirement."
			);
		}
	}
}

public class WithMoveSound : INotifyCreated, INotifyMoving, INotifyRemovedFromWorld
{
	private readonly WithMoveSoundInfo info;
	private Aircraft? aircraft;
	private Mobile? mobile;
	private WithWorldMoveSound? worldTrait;

	public WithMoveSound(WithMoveSoundInfo info)
	{
		this.info = info;
	}

	void INotifyCreated.Created(Actor self)
	{
		this.worldTrait = self.World.WorldActor.TraitOrDefault<WithWorldMoveSound>();
		this.mobile = self.TraitOrDefault<Mobile>();
		this.aircraft = self.TraitOrDefault<Aircraft>();
	}

	void INotifyMoving.MovementTypeChanged(Actor self, MovementType type)
	{
		var enableSound = false;

		if (this.mobile != null)
			enableSound = type != MovementType.None && this.mobile.IsMovingBetweenCells;
		else if (this.aircraft != null)
			enableSound = type != MovementType.None && !this.aircraft.AtLandAltitude;

		if (enableSound)
			this.worldTrait?.Enable(self, this.info.Sound);
		else
			this.worldTrait?.Disable(self, this.info.Sound);
	}

	void INotifyRemovedFromWorld.RemovedFromWorld(Actor self)
	{
		this.worldTrait?.Disable(self, this.info.Sound);
	}
}
