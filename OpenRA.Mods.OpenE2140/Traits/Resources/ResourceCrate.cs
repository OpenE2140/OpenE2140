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
using OpenRA.Mods.OpenE2140.Traits.SubActors;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

[UsedImplicitly]
[Desc("This actor is a resource crate.")]
public class ResourceCrateInfo : TraitInfo, IEditorActorOptions, Requires<SubActorInfo>
{
	[Desc("Display order for the initial resources slider in the map editor")]
	public readonly int EditorInitialResourcesDisplayOrder = 4;

	[Desc("Maximum amount of the initial resources slider in the map editor")]
	public readonly int EditorMaximumInitialResources = 5;

	public override object Create(ActorInitializer init)
	{
		return new ResourceCrate(init);
	}

	IEnumerable<EditorActorOption> IEditorActorOptions.ActorOptions(ActorInfo ai, OpenRA.World world)
	{
		yield return new EditorActorSlider("Resources", this.EditorInitialResourcesDisplayOrder, 0,
			maxValue: this.EditorMaximumInitialResources,
			ticks: Math.Min(20, this.EditorMaximumInitialResources),
			actor =>
			{
				var init = actor.GetInitOrDefault<ResourcesInit>(this);
				if (init != null)
					return init.Value;

				return this.EditorMaximumInitialResources;
			},
			(actor, value) => actor.ReplaceInit(new ResourcesInit((int)value), this));
	}
}

public class ResourceCrate
{
	public readonly Actor Actor;
	public readonly SubActor SubActor;

	public Actor? ReservedTransporter { get; private set; }

	public int Resources;

	public ResourceCrate(ActorInitializer init)
	{
		this.Actor = init.Self;
		this.SubActor = this.Actor.Trait<SubActor>();

		var resourcesInit = init.GetOrDefault<ResourcesInit>();
		if (resourcesInit != null)
			this.Resources = resourcesInit.Value;
	}

	public bool ReserveTransporter(Actor transporterActor)
	{
		if (this.ReservedTransporter == transporterActor)
			return true;

		if (this.ReservedTransporter == null)
		{
			this.ReservedTransporter = transporterActor;
			return true;
		}

		return false;
	}

	public void UnreserveTransporter()
	{
		if (this.ReservedTransporter == null)
			return;

		this.ReservedTransporter = null;
	}
}

public class ResourcesInit : ValueActorInit<int>, ISingleInstanceInit
{
	public ResourcesInit(int value)
		: base(value) { }

	public override int Value => Math.Max(0, base.Value);
}
