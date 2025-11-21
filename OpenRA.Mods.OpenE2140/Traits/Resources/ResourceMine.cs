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

using System.Collections.Immutable;
using JetBrains.Annotations;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.OpenE2140.Traits;

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

	[Desc("If true, cells being mined are shuffled before each mining tick.")]
	public readonly bool ShuffleMinableCells = false;

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

	public IEnumerable<CPos> GetCellsInMiningArea(CPos centerCell)
	{
		var range = this.Range;

		for (var y = -range; y <= range; y++)
		{
			for (var x = -range; x <= range; x++)
			{
				var vec = new CVec(y, x);

				if (vec.Length <= range)
					yield return centerCell + vec;
			}
		}
	}
}

public class ResourceMine : ConveyorBelt
{
	public new readonly ResourceMineInfo Info;

	private readonly IResourceLayer? resourceLayer;

	private int delay;
	private ResourceCrate? crateBeingMined;
	private ResourceCrate? availableCrate;

	public IReadOnlyList<CPos> CellsInMiningArea { get; }

	public bool IsDepleted { get; private set; }

	public ResourceMine(Actor self, ResourceMineInfo info)
		: base(info)
	{
		this.Info = info;

		this.resourceLayer = self.World.WorldActor.TraitOrDefault<ResourceLayer>();

		var customBuildingInfo = self.Info.TraitInfoOrDefault<ICustomBuildingInfo>();

		// Mineable cells never change (because Mine's position cannot change), so precalculate them on creation
		this.CellsInMiningArea = this.Info.GetCellsInMiningArea(
				self.World.Map.CellContaining(customBuildingInfo.GetCenterOfFootprint(self.Location))
			).ToImmutableList();
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
				[new ParentActorInit(self), new LocationInit(self.Location), new OwnerInit(self.Owner)]
			)
			.Trait<ResourceCrate>();

		var minable = Math.Min(this.Info.Force, this.Info.CrateSize - this.crateBeingMined.Resources);

		if (minable > 0)
		{
			var mined = 0;

			IEnumerable<CPos> minableCells = this.CellsInMiningArea;
			if (this.Info.ShuffleMinableCells)
				minableCells = minableCells.Shuffle(self.World.SharedRandom);

			foreach (var targetCell in minableCells)
			{
				mined += this.resourceLayer.RemoveResource(this.resourceLayer.GetResource(targetCell).Type, targetCell, minable - mined);
				if (mined >= minable)
					break;
			}

			if (mined == 0)
			{
				this.IsDepleted = true;

				mined = this.Info.EmptyForce;
			}

			this.crateBeingMined.Resources += mined;
		}

		if (this.crateBeingMined.Resources < this.Info.CrateSize)
			return;

		if (this.Activate(self, this.crateBeingMined))
			this.crateBeingMined = null;
	}

	protected override void Complete(Actor self, ResourceCrate crate)
	{
		this.availableCrate = crate;
	}

	protected override bool TryAcquireLockInner(Actor clientActor)
	{
		return this.availableCrate != null;
	}

	public ResourceCrate? RemoveCrate()
	{
		if (this.availableCrate == null)
			return null;

		var crate = this.availableCrate;

		if (crate != null)
		{
			this.availableCrate = null;
			this.OnCrateProcessed();

			crate.SubActor.ParentActor = null;
		}

		return crate;
	}
}
