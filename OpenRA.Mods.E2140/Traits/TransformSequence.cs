#region Copyright & License Information

/*
 * Copyright 2007-2022 The Earth 2140 Developers (see AUTHORS)
 * This file is part of Earth 2140, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */

#endregion

using JetBrains.Annotations;
using OpenRA.Traits;

namespace OpenRA.Mods.E2140.Traits;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class TransformSequenceInfo : TraitInfo
{
	public readonly string? Suffix;

	public override object Create(ActorInitializer init)
	{
		return new TransformSequence();
	}
}

public class TransformSequence
{
	public void Run()
	{
		Console.WriteLine("grant a condition so the actor can disable its stuff");
		Console.WriteLine("play core_buildingsequence_{Suffix}.deploy and wait on last frame");
		Console.WriteLine("when last frame is reached, play cover_building");
		Console.WriteLine("when finished, wait a specific delay and remove the deploy animation");
		Console.WriteLine("when delay finished, revoke the condition, and play cover_building in reverse once");
	}
}
