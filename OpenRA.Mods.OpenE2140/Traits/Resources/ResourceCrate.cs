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
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

[UsedImplicitly]
[Desc("This actor is a resource crate.")]
public class ResourceCrateInfo : TraitInfo
{
	public override object Create(ActorInitializer init)
	{
		return new ResourceCrate(init.Self);
	}
}

public class ResourceCrate
{
	public readonly Actor Actor;

	public int Resources;

	public ResourceCrate(Actor self)
	{
		this.Actor = self;
	}
}
