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

using OpenRA.Primitives;

namespace OpenRA.Mods.OpenE2140.Traits;

public interface ISafeDragNotify
{
	void SafeDragFailed(Actor self, Actor movingActor);

	void SafeDragComplete(Actor self, Actor movingActor);
}

/// <summary>
/// Hook for modifying actor init objects in <see cref="TypeDictionary"/> before the actor is created by <see cref="Production.AnimatedExitProduction"/>.
/// </summary>
public interface IProduceActorInitModifier
{
    /// <summary>
    /// This hook is called just before the actor is created and makes it possible to modify actor init objects inside <see cref="TypeDictionary"/>.
    /// </summary>
    /// <remarks>
    /// The exact location, where the is hook called, is just before invoking
    /// <see cref="Common.Traits.Production.DoProduction(Actor, ActorInfo, Common.Traits.ExitInfo, string, TypeDictionary)"/> method.
    /// It means that this method can override any changes done by this hook.
    /// </remarks>
    void ModifyActorInit(Actor self, TypeDictionary init);
}
