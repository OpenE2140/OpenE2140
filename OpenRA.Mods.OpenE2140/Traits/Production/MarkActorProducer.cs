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

using OpenRA.Mods.Common;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Production;

[Desc($"Marks all produced actors with the actor that produced them by adding {nameof(ActorProducerInit)} to {nameof(TypeDictionary)} the actor is created with.")]
public class MarkActorProducerInfo : TraitInfo<MarkActorProducer>
{
}

public class MarkActorProducer : IProduceActorInitModifier
{
	void IProduceActorInitModifier.ModifyActorInit(Actor self, TypeDictionary init)
	{
		init.Add(new ActorProducerInit(self));
	}
}

public class ActorProducerInit : ValueActorInit<ActorInitActorReference>, ISingleInstanceInit
{
	public ActorProducerInit(Actor actor)
		: base(actor) { }
}
