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
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits;

[UsedImplicitly]
[Desc("Add to MCU to inform deployed actor it was created by MCU deployment.")]
public class McuInfo : TraitInfo
{
	public override object Create(ActorInitializer init)
	{
		return new Mcu();
	}
}

public class Mcu : ITransformActorInitModifier
{
	public class McuInit : ActorInit, ISingleInstanceInit
	{
		public override MiniYaml Save()
		{
			return new MiniYaml(string.Empty);
		}
	}

	void ITransformActorInitModifier.ModifyTransformActorInit(Actor self, TypeDictionary init)
	{
		init.Add(new McuInit());
	}
}
