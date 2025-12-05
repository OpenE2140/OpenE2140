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

using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Primitives;
using OpenRA.Support;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits;

public class WaterBaseBuildingInfo : CustomBuildingInfo, Requires<RepairableBuildingInfo>, INotifyEditorPlacementInfo
{
	public override Dictionary<CPos, PlaceBuildingCellType> GetBuildingPlacementFootprint(World world, CPos cell, Actor toIgnore)
	{
		var footprint = base.GetBuildingPlacementFootprint(world, cell, toIgnore);

		var footprintCellTypes = new Dictionary<string, int>();
		foreach ((var c, var type) in footprint)
		{
			var terrainType = world.Map.GetTerrainInfo(c).Type;
			footprintCellTypes.TryGetValue(terrainType, out var count);
			footprintCellTypes[terrainType] = ++count;
		}

		// Next check, if cells of the footprint contain valid terrain types
		if (this.AllowedTerrainTypesCondition?.Evaluate(footprintCellTypes) == false)
			return footprint.Keys.ToDictionary(c => c, _ => PlaceBuildingCellType.Invalid);

		return footprint;
	}

	public override object Create(ActorInitializer init)
	{
		return new WaterBaseBuilding(init.Self);
	}

	object? INotifyEditorPlacementInfo.AddedToEditor(EditorActorPreview preview, World editorWorld)
	{
		editorWorld.WorldActor.Trait<WaterBaseEditor>().OnActorAdded(preview);

		return null;
	}

	void INotifyEditorPlacementInfo.RemovedFromEditor(EditorActorPreview preview, World editorWorld, object data) { }
}

public class WaterBaseBuilding : CustomBuilding, INotifyOwnerChanged, INotifySelected, INotifyDamage, INotifyBuildingRepair, IObservesVariables
{
	public const string WaterBaseDamageSyncType = "WaterBaseDamageSync";

	private static readonly BooleanExpression PoweredDown = new BooleanExpression("PoweredDown");

	private readonly Actor self;
	private readonly RepairableBuilding repairBuilding;
	private readonly ConditionWatcher watcher;

	public Actor? DockActor { get; private set; }
	public bool IsRepairActive => this.repairBuilding.RepairActive;

	private WaterBaseDock? waterBaseDock;

	public WaterBaseBuilding(Actor self)
	{
		this.self = self;
		this.repairBuilding = self.Trait<RepairableBuilding>();
		this.watcher = new ConditionWatcher()
			.Watch(PoweredDown, x => this.waterBaseDock?.OnBasePoweredDown(x));
	}

	IEnumerable<VariableObserver> IObservesVariables.GetVariableObservers() => this.watcher.GetVariableObservers();

	internal void AssignDock(Actor self, Actor dockActor)
	{
		this.DockActor = dockActor;
		this.waterBaseDock = dockActor.Trait<WaterBaseDock>();

		if (self.Owner != dockActor.Owner)
			dockActor.ChangeOwner(self.Owner);
	}

	void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
	{
		if (this.DockActor?.Owner != newOwner)
			this.DockActor?.ChangeOwner(newOwner);
	}

	void INotifySelected.Selected(Actor self)
	{
		this.waterBaseDock?.OnBaseSelected();
	}

	internal void OnSelected()
	{
		this.self.World.Selection.TryAdd(this.self);
	}

	void INotifyDamage.Damaged(Actor self, AttackInfo e)
	{
		this.waterBaseDock?.OnBaseDamaged(e);
	}

	internal void OnDockDamaged(AttackInfo e)
	{
		if (e.Damage.DamageTypes.Contains(WaterBaseBuilding.WaterBaseDamageSyncType))
			return;

		var damageType = e.Damage.DamageTypes.Union(new BitSet<DamageType>(WaterBaseDamageSyncType));
		this.self.InflictDamage(e.Attacker, new Damage(e.Damage.Value, damageType));
	}

	void INotifyBuildingRepair.RepairStarted(Actor self)
	{
		if (this.waterBaseDock?.IsRepairActive == false)
			this.waterBaseDock.OnBaseRepairStarted();
	}

	void INotifyBuildingRepair.RepairInterrupted(Actor self)
	{
		if (this.waterBaseDock?.IsRepairActive == true)
			this.waterBaseDock.OnBaseRepairInterrupted();
	}

	internal void OnDockRepairStarted()
	{
		this.repairBuilding.RepairBuilding(this.self, this.self.Owner);
	}

	internal void OnDockRepairInterrupted()
	{
		this.repairBuilding.RepairBuilding(this.self, this.self.Owner);
	}

	internal void OnDockPoweredDown(bool isEnabled)
	{
		if (this.watcher.IsEnabled(PoweredDown) != isEnabled)
			WaterBaseUtils.TogglePoweredDownState(this.self);
	}
}
