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

namespace OpenRA.Mods.E2140.Traits.Construction;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("Add to MCU to trigger the TransformSequence trait.")]
public class TriggerTransformSequenceInfo : TraitInfo
{
	public override object Create(ActorInitializer init)
	{
		return new TriggerTransformSequence();
	}
}

public class TriggerTransformSequence : INotifyTransform
{
	void INotifyTransform.BeforeTransform(Actor self)
	{
	}

	void INotifyTransform.OnTransform(Actor self)
	{
	}

	void INotifyTransform.AfterTransform(Actor toActor)
	{
		toActor.TraitOrDefault<TransformSequence>().Run(toActor);
	}
}
