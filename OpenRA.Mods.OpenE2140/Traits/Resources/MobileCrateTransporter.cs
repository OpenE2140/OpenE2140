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

using OpenRA.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Traits.Resources.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

public class MobileCrateTransporterInfo : CrateTransporterInfo, Requires<MobileInfo>
{
	[Desc("Percentage modifier to apply to movement speed while docking to conveyor belt or (un)loading crate to/from ground.")]
	public readonly int DockSpeedModifier = 70;

	[Desc("Sequence for crate movement during load animation")]
	[FieldLoader.LoadUsing(nameof(LoadLoadCrateMoveSequence), true)]
	public readonly CrateMoveSequence LoadSequence = null!;

	[Desc("Sequence for crate movement during unload animation")]
	[FieldLoader.LoadUsing(nameof(LoadUnloadCrateMoveSequence), required: true)]
	public readonly CrateMoveSequence UnloadSequence = null!;

	private static object LoadLoadCrateMoveSequence(MiniYaml parentNode)
	{
		return CrateMoveSequence.Load(parentNode, nameof(LoadSequence));
	}

	private static object LoadUnloadCrateMoveSequence(MiniYaml parentNode)
	{
		return CrateMoveSequence.Load(parentNode, nameof(UnloadSequence));
	}

	public override object Create(ActorInitializer init)
	{
		return new MobileCrateTransporter(init, this);
	}
}

public class MobileCrateTransporter : CrateTransporter
{
	public new MobileCrateTransporterInfo Info;
	private readonly Mobile mobile;

	public MobileCrateTransporter(ActorInitializer init, MobileCrateTransporterInfo info)
		: base(init, info)
	{
		this.Info = info;

		this.mobile = init.Self.Trait<Mobile>();
	}

	protected override Activity GetCrateUnloadActivity(Actor self, Order order)
	{
		CPos? targetLocation = order.Target.Type != TargetType.Invalid ? self.World.Map.CellContaining(order.Target.CenterPosition) : null;
		return new MobileCrateUnload(self, targetLocation);
	}

	protected override Activity GetCrateLoadActivity(Actor self, Order order)
	{
		return new MobileCrateLoad(self, order.Target);
	}

	internal void ReserveUnloadLocation(CPos targetLocation)
	{
		if (!this.HasCrate)
			return;

		// We need to block target cell (until the unload is complete) so that no other actor can enter it while the unloading is in progress.
		// Current solution makes use of CrateTransporter's Mobile implementing IOccupySpace by returning both FromCell and ToCell (if they differ).

		// Unfortunately this hacky solution is necessary. If the cell is to be blocked by the crate itself,
		// it's necessary to add the crate actor to the world and that causes more issues (when it comes to the crate unload feature).
		// So the crate is added to world at the very last moment: i.e. when the crate (SubActor) is detached from CrateTransporter.
		this.mobile.SetLocation(targetLocation, SubCell.FullCell, this.mobile.ToCell, SubCell.FullCell);
	}

	internal override void UnloadComplete()
	{
		this.mobile.SetLocation(this.mobile.ToCell, SubCell.FullCell, this.mobile.ToCell, SubCell.FullCell);
	}
}
