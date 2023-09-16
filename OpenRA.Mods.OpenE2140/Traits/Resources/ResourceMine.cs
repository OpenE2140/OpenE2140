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

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("This actor can extract resources and eject resource crates.")]
public class ResourceMineInfo : PausableConditionalTraitInfo
{
	[Desc("The maximum range this actor can dig for resources.")]
	public readonly int Range = 5;

	[Desc("The amount of resources which can be mined per tick.")]
	public readonly int Force = 5;

	public override object Create(ActorInitializer init)
	{
		return new ResourceMine(this, init.World);
	}
}

public class ResourceMine : PausableConditionalTrait<ResourceMineInfo>, INotifyAddedToWorld, INotifyOwnerChanged, ITick
{
	private readonly IResourceLayer? resourceLayer;

	private PlayerResources? playerResources;

	public ResourceMine(ResourceMineInfo info, OpenRA.World world)
		: base(info)
	{
		this.resourceLayer = world.WorldActor.TraitOrDefault<ResourceLayer>();
	}

	void INotifyAddedToWorld.AddedToWorld(Actor self)
	{
		this.playerResources = self.Owner.PlayerActor.TraitOrDefault<PlayerResources>();
	}

	void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
	{
		this.playerResources = newOwner.PlayerActor.TraitOrDefault<PlayerResources>();
	}

	void ITick.Tick(Actor self)
	{
		if (this.IsTraitPaused || this.resourceLayer == null || this.playerResources == null)
			return;

		var centerCell = self.World.Map.CellContaining(self.CenterPosition);

		var remaining = this.Info.Force;

		for (var y = -this.Info.Range; y <= this.Info.Range && remaining > 0; y++)
		for (var x = -this.Info.Range; x <= this.Info.Range && remaining > 0; x++)
		{
			var targetCell = centerCell + new CVec(y, x);

			if (Math.Abs((targetCell - centerCell).Length) > this.Info.Range)
				continue;

			var resource = this.resourceLayer.GetResource(targetCell);
			remaining -= this.resourceLayer.RemoveResource(resource.Type, targetCell, remaining);
		}

		this.playerResources.GiveCash(this.Info.Force - remaining);
	}
}
