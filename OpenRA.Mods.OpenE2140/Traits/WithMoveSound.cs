#region Copyright & License Information

/*
 * Copyright 2007-2023 The OpenE2140 Developers (see AUTHORS)
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

public class WithMoveSound : INotifyMoving, ITick, INotifyRemovedFromWorld
{
	private readonly WithMoveSoundInfo info;
	private readonly Mobile mobile;
	private ISound? sound;

	public WithMoveSound(WithMoveSoundInfo info, ActorInitializer init)
	{
		this.info = info;
		this.mobile = init.Self.Trait<Mobile>();
	}

	void INotifyMoving.MovementTypeChanged(Actor self, MovementType type)
	{
		if (type != MovementType.None && mobile.IsMovingBetweenCells)
			this.sound ??= Game.Sound.PlayLooped(SoundType.World, this.info.Sound, self.CenterPosition);
		else
		{
			Game.Sound.EndLoop(this.sound);
			this.sound = null;
		}
	}

	void ITick.Tick(Actor self)
	{
		this.sound?.SetPosition(self.CenterPosition);
	}

	void INotifyRemovedFromWorld.RemovedFromWorld(Actor self)
	{
		Game.Sound.EndLoop(this.sound);
		this.sound = null;
	}
}
