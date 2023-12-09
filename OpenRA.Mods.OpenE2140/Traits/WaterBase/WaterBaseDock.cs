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
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Mods.OpenE2140.Traits.Misc;
using OpenRA.Mods.OpenE2140.Traits.World.Editor;
using OpenRA.Primitives;
using OpenRA.Support;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.WaterBase;

public class WaterBaseDockInfo : TraitInfo, Requires<RepairableBuildingInfo>, IEditorActorOptions, INotifyEditorPlacementInfo
{
	public override object Create(ActorInitializer init)
	{
		return new WaterBaseDock(init, this);
	}

	IEnumerable<EditorActorOption> IEditorActorOptions.ActorOptions(ActorInfo ai, OpenRA.World world)
	{
		return world.WorldActor.Trait<WaterBaseEditor>().GetWaterDockActorOptions(ai, world, this);
	}

	object? INotifyEditorPlacementInfo.AddedToEditor(EditorActorPreview preview, OpenRA.World editorWorld)
	{
		editorWorld.WorldActor.Trait<WaterBaseEditor>().OnActorAdded(preview);

		return null;
	}

	void INotifyEditorPlacementInfo.RemovedFromEditor(EditorActorPreview preview, OpenRA.World editorWorld, object data)
	{
		editorWorld.WorldActor.Trait<WaterBaseEditor>().OnActorRemoved(preview);
	}
}

public class WaterBaseDock : INotifySelected, INotifyDamage, INotifyBuildingRepair, IObservesVariables
{
	public const string WaterBaseDockFakeRepairDamageType = "FakeRepair";

	private static readonly BooleanExpression PoweredDown = new BooleanExpression("PoweredDown");

	private readonly Actor self;
	private readonly RepairableBuilding repairBuilding;
	private readonly ConditionWatcher watcher;

	// TODO: shouldn't this be not null?
	private WaterBaseBuilding? waterBaseBuilding;

	public bool IsRepairActive => this.repairBuilding.RepairActive;

	public WaterBaseDock(ActorInitializer init, WaterBaseDockInfo info)
	{
		this.self = init.Self;
		// TODO: there should always be this init object (either from map, or passed when Dock actor is created in WaterBaseBuilding
		var waterBaseActor = init.GetOrDefault<WaterBaseDockInit>(info);
		if (waterBaseActor != null)
		{
			init.World.AddFrameEndTask(_ =>
			{
				var buildingActor = waterBaseActor.Value.Actor(init.World).Value;
				this.waterBaseBuilding = buildingActor.Trait<WaterBaseBuilding>();
				this.waterBaseBuilding.AssignDock(buildingActor, init.Self);
			});
		}
		this.repairBuilding = this.self.Trait<RepairableBuilding>();
		this.watcher = new ConditionWatcher()
			.Watch(PoweredDown, x => this.waterBaseBuilding?.OnDockPoweredDown(x));
	}

	IEnumerable<VariableObserver> IObservesVariables.GetVariableObservers() => this.watcher.GetVariableObservers();

	void INotifyDamage.Damaged(Actor self, AttackInfo e)
	{
		this.waterBaseBuilding?.OnDockDamaged(e);
	}

	internal void OnBaseDamaged(AttackInfo e)
	{
		if (e.Damage.DamageTypes.Contains(WaterBaseBuilding.WaterBaseDamageSyncType) ||
			e.Damage.DamageTypes.Contains(WaterBaseDock.WaterBaseDockFakeRepairDamageType))
			return;

		var damageType = e.Damage.DamageTypes.Union(new BitSet<DamageType>(WaterBaseBuilding.WaterBaseDamageSyncType));
		this.self.InflictDamage(e.Attacker, new Damage(e.Damage.Value, damageType));
	}

	void INotifySelected.Selected(Actor self)
	{
		this.waterBaseBuilding?.OnSelected();
	}

	internal void OnBaseSelected()
	{
		this.self.World.Selection.TryAdd(this.self);
	}

	void INotifyBuildingRepair.RepairStarted(Actor self)
	{
		if (this.waterBaseBuilding?.IsRepairActive == false)
			this.waterBaseBuilding?.OnDockRepairStarted();
	}

	void INotifyBuildingRepair.RepairInterrupted(Actor self)
	{
		if (this.waterBaseBuilding?.IsRepairActive == true)
			this.waterBaseBuilding?.OnDockRepairInterrupted();
	}

	internal void OnBaseRepairStarted()
	{
		this.repairBuilding.RepairBuilding(this.self, this.self.Owner);
	}

	internal void OnBaseRepairInterrupted()
	{
		this.repairBuilding.RepairBuilding(this.self, this.self.Owner);
	}

	internal void OnBasePoweredDown(bool isEnabled)
	{
		if (this.watcher.IsEnabled(PoweredDown) != isEnabled)
			WaterBaseUtils.TogglePoweredDownState(this.self);
	}
}

public class WaterBaseDockInit : ValueActorInit<ActorInitActorReference>, ISingleInstanceInit
{
	public WaterBaseDockInit(Actor actor)
		: base(actor) { }
	public WaterBaseDockInit(ActorInitActorReference actorInitActorReference)
		: base(actorInitActorReference) { }
}

