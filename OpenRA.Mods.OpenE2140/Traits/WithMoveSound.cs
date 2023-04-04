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

namespace OpenRA.Mods.OpenE2140.Traits;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("Makes an actor play a sound while moving.")]
public class WithMoveSoundInfo : TraitInfo, Requires<MobileInfo>
{
	[FieldLoader.RequireAttribute]
	public readonly string Sound = "";

	public override object Create(ActorInitializer init)
	{
		return new WithMoveSound(this, init);
	}
}

public class WithMoveSound : INotifyCreated, INotifyMoving, INotifyRemovedFromWorld
{
	private readonly WithMoveSoundInfo info;
	private readonly Mobile mobile;
	private WithWorldMoveSound? worldTrait;

	public WithMoveSound(WithMoveSoundInfo info, ActorInitializer init)
	{
		this.info = info;
		this.mobile = init.Self.Trait<Mobile>();
	}

	void INotifyCreated.Created(Actor self)
	{
		this.worldTrait = self.World.WorldActor.TraitOrDefault<WithWorldMoveSound>();
	}

	void INotifyMoving.MovementTypeChanged(Actor self, MovementType type)
	{
		if (type != MovementType.None && this.mobile.IsMovingBetweenCells)
			this.worldTrait?.Enable(self, this.info.Sound);
		else
			this.worldTrait?.Disable(self, this.info.Sound);
	}

	void INotifyRemovedFromWorld.RemovedFromWorld(Actor self)
	{
		this.worldTrait?.Disable(self, this.info.Sound);
	}
}
