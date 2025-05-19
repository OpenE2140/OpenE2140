#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.OpenE2140.Traits.World;

[TraitLocation(SystemActors.World)]
[Desc("Automatically selects the first available production tab. Makes the production sidebar always show a queue if any valid one exists. Attach to World actor.")]
public class AutoSelectProductionQueueInfo : TraitInfo
{
	[FieldLoader.Require]
	public readonly string ProductionTabsWidget = null!;

	[FieldLoader.Require]
	public readonly string ProductionPaletteWidget = null!;

	public override object Create(ActorInitializer init) { return new AutoSelectProductionQueue(this); }
}

public class AutoSelectProductionQueue : INotifyAddedToWorld, INotifyRemovedFromWorld, ITick
{
	private readonly Lazy<ProductionTabsWidget?> tabsWidget;
	private readonly Lazy<ProductionPaletteWidget?> paletteWidget;

	private readonly List<TraitPair<ProductionQueue>> productionQueuesToCheck = [];

	public AutoSelectProductionQueue(AutoSelectProductionQueueInfo info)
	{
		this.tabsWidget = Exts.Lazy(() => Ui.Root.GetOrNull(info.ProductionTabsWidget) as ProductionTabsWidget);
		this.paletteWidget = Exts.Lazy(() => Ui.Root.GetOrNull(info.ProductionPaletteWidget) as ProductionPaletteWidget);
	}

	void INotifyAddedToWorld.AddedToWorld(Actor self)
	{
		self.World.ActorAdded += this.ActorChanged;
		self.World.ActorRemoved += this.ActorChanged;
	}

	void INotifyRemovedFromWorld.RemovedFromWorld(Actor self)
	{
		self.World.ActorAdded += this.ActorChanged;
		self.World.ActorRemoved += this.ActorChanged;
	}

	private void ActorChanged(Actor actor)
	{
		// Ignore non-production actors and actors owned by non-local player
		if (!actor.Info.HasTraitInfo<ProductionQueueInfo>() || actor.Owner != actor.World.LocalPlayer)
			return;

		if (actor.IsDead || !actor.IsInWorld)
			this.productionQueuesToCheck.RemoveAll(p => p.Actor == actor);
		else
			this.productionQueuesToCheck.AddRange(actor.TryGetTraitsImplementing<ProductionQueue>().Select(t => new TraitPair<ProductionQueue>(actor, t)));
	}

	void ITick.Tick(Actor self)
	{
		if (this.tabsWidget.Value == null || this.paletteWidget.Value == null || this.productionQueuesToCheck.Count == 0)
			return;

		// Don't touch currently selected queue
		if (this.tabsWidget.Value.CurrentQueue != null || this.paletteWidget.Value!.CurrentQueue != null)
			return;

		// Queue-per-player not supported
		var queueSelected = false;
		foreach (var (actor, queue) in this.productionQueuesToCheck)
		{
			if (actor.Disposed || !queue.Enabled || !queue.IsTraitEnabled() || !queue.AnyItemsToBuild())
				continue;

			this.tabsWidget.Value.CurrentQueue = queue;
			this.paletteWidget.Value.CurrentQueue = queue;

			queueSelected = true;
			break;
		}

		// There's no need to keep checking existing set of production queues. Production widgets detect, if the producing actor has been killed/disposed and
		// automatically select the next available tab. If a new actor with production queue is created, it is handled in ActorChanged event handler as usual.
		if (queueSelected)
			this.productionQueuesToCheck.Clear();
	}
}
