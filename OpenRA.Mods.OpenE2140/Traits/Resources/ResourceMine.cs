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

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("This actor can extract resources and eject resource crates.")]
public class ResourceMineInfo : ConveyorBeltInfo
{
	[Desc("The maximum range this actor can dig for resources.")]
	public readonly int Range = 5;

	[Desc("The amount of resources which can be mined per tick.")]
	public readonly int Force = 5;

	[Desc("The amount of resources which can be mined per tick when empty.")]
	public readonly int EmptyForce = 1;

	[Desc("The amount of ticks between mining.")]
	public readonly int Delay = 1;

	[Desc("The amount of resources that will be put into a single crate.")]
	public readonly int CrateSize = 500;

	[Desc("The resource crate actor.")]
	public readonly string CrateActor = "crate";

	public override object Create(ActorInitializer init)
	{
		return new ResourceMine(init.Self, this);
	}
}

public class ResourceMine : ConveyorBelt
{
	public new readonly ResourceMineInfo Info;

	private readonly IResourceLayer? resourceLayer;

	private int delay;
	private ResourceCrate? crateBeingMined;

	public ResourceMine(Actor self, ResourceMineInfo info)
		: base(info)
	{
		this.Info = info;

		this.resourceLayer = self.World.WorldActor.TraitOrDefault<ResourceLayer>();
	}

	protected override void TickInner(Actor self)
	{
		base.TickInner(self);

		if (this.IsTraitDisabled || this.IsTraitPaused || this.resourceLayer == null)
			return;

		this.delay = (this.delay + 1) % (this.Info.Delay + 1);

		if (this.delay != 0)
			return;

		this.crateBeingMined ??= self.World.CreateActor(
				false,
				this.Info.CrateActor,
				new TypeDictionary { new ParentActorInit(self), new LocationInit(self.Location), new OwnerInit(self.Owner) }
			)
			.Trait<ResourceCrate>();

		var minable = Math.Min(this.Info.Force, this.Info.CrateSize - this.crateBeingMined.Resources);

		if (minable > 0)
		{
			var mined = this.Info.EmptyForce;
			var centerCell = self.World.Map.CellContaining(self.CenterPosition);

			for (var y = -this.Info.Range; y <= this.Info.Range && mined < minable; y++)
			{
				for (var x = -this.Info.Range; x <= this.Info.Range && mined < minable; x++)
				{
					var targetCell = centerCell + new CVec(y, x);

					if ((targetCell - centerCell).Length <= this.Info.Range)
						mined += this.resourceLayer.RemoveResource(this.resourceLayer.GetResource(targetCell).Type, targetCell, minable - mined);
				}
			}

			this.crateBeingMined.Resources += mined;
		}

		if (this.crateBeingMined.Resources < this.Info.CrateSize)
			return;

		if (this.Activate(self, this.crateBeingMined))
			this.crateBeingMined = null;
	}

	public ResourceCrate? RemoveCrate()
	{
		if (this.crate == null || this.crate?.Resources < this.Info.CrateSize)
			return null;

		var crate = this.crate;

		if (crate != null)
		{
			this.crate = null;

			crate.SubActor.ParentActor = null;
		}

		return crate;
	}
}
