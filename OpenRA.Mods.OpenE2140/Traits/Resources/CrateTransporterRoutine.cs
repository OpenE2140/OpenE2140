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
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Activites.Move;
using OpenRA.Mods.OpenE2140.Traits.Resources.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("Handles CrateTransporter's routine for transporting resource crates.")]
public class CrateTransporterRoutineInfo : TraitInfo, Requires<CrateTransporterInfo>
{
	[Desc("If true, CrateTransporter automatically starts the transport routine, when it is produced.")]
	public readonly bool StartWhenProduced = true;

	[Desc("If true, CrateTransporter will automatically find Refinery or Mine to dock with.")]
	public readonly bool AssignTargetsAutomatically = true;

	[Desc($"Delay start of the routine when created using {nameof(FreeActor)} trait. -1 means don't start routine at all.")]
	public readonly int FreeActorDelayRoutine = 0;

	public override object Create(ActorInitializer init)
	{
		return new CrateTransporterRoutine(init, this);
	}
}

public class CrateTransporterRoutine : INotifyDockClient, IResolveOrder, INotifyActorProduced, INotifyCreated
{
	public readonly CrateTransporterRoutineInfo Info;

	private readonly bool startRoutine;

	public CrateTransporterRoutine(ActorInitializer init, CrateTransporterRoutineInfo info)
	{
		this.Info = info;

		this.startRoutine = init.GetOrDefault<ParentActorInit>(info) != null;
	}

	public Actor? CurrentMine { get; private set; }
	public Actor? CurrentRefinery { get; private set; }

	public void StartTransporterRoutine(Actor self)
	{
		self.CurrentActivity?.Cancel(self);

		self.QueueActivity(new TransportCrates(self));
	}

	void INotifyCreated.Created(Actor self)
	{
		if (this.startRoutine && this.Info.FreeActorDelayRoutine >= 0)
		{
			if (this.Info.FreeActorDelayRoutine > 0)
				self.QueueActivity(new Wait(this.Info.FreeActorDelayRoutine));

			self.QueueActivity(new TransportCrates(self));
		}
	}

	void INotifyDockClient.Docked(Actor self, Actor host)
	{
		// noop
	}

	void INotifyDockClient.Undocked(Actor self, Actor host)
	{
		if (this.CurrentMine == null && host.Info.HasTraitInfo<ResourceMineInfo>())
			this.CurrentMine = host;
		else if (this.CurrentRefinery == null && host.Info.HasTraitInfo<ResourceRefineryInfo>())
			this.CurrentRefinery = host;

		var currentActivity = self.CurrentActivity;
		if (currentActivity == null || (currentActivity is MoveToDock or TransportCrates && currentActivity.NextActivity == null))
			self.QueueActivity(new TransportCrates(self));
	}

	void IResolveOrder.ResolveOrder(Actor self, Order order)
	{
		if (order.OrderString == "Dock" || order.OrderString == "ForceDock")
		{
			if (order.Target.Type != TargetType.Actor)
				return;

			var actor = order.Target.Actor;

			if (actor.Info.HasTraitInfo<ResourceMineInfo>())
				this.CurrentMine = actor;
			else if (actor.Info.HasTraitInfo<ResourceRefineryInfo>())
				this.CurrentRefinery = actor;
		}
		else
		{
			this.CurrentMine = null;
			this.CurrentRefinery = null;
		}
	}

	void INotifyActorProduced.OnProduced(Actor self, Actor producent)
	{
		if (!this.Info.StartWhenProduced)
			return;

		var currentActivity = self.CurrentActivity;

		// While Mobile actors have ProductionExitMove queued when produced, for Aircraft it is the Fly activity.
		// Currently, when the production building has rally point set, the TransportCrates activity is not queued.
		if (currentActivity == null || (currentActivity is ProductionExitMove or Fly && currentActivity.NextActivity == null))
			self.QueueActivity(new TransportCrates(self));
	}
}
