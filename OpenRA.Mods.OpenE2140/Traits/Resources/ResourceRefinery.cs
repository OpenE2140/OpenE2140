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
using OpenRA.Activities;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

[UsedImplicitly]
[Desc("This actor can accept resource crates and process them.")]
public class ResourceRefineryInfo : ConveyorBeltInfo
{
	[Desc("Additional time required for crate processing after the crate disappears inside the Refinery.")]
	public readonly int DelayCrateProcessing = 50;

	[Desc("Maximum number of crates Refinery can buffer for processing.")]
	public readonly int BufferCapacity = 1;

	[GrantedConditionReference]
	[Desc("Condition to grant while a resource crate is being processed.")]
	public readonly string ProcessCrateCondition = "ProcessingCrate";

	public override object Create(ActorInitializer init)
	{
		return new ResourceRefinery(this);
	}
}

public class ResourceRefinery : ConveyorBelt, INotifyAddedToWorld, INotifyOwnerChanged
{
	private readonly ResourceRefineryInfo info;
	private readonly List<ResourceCrate> processingBuffer;

	private PlayerResources? playerResources;
	private int crateProcessingToken = Actor.InvalidConditionToken;

	private int BufferCapacity => this.info.BufferCapacity;

	public ResourceRefinery(ResourceRefineryInfo info)
		: base(info)
	{
		this.info = info;

		this.processingBuffer = new(this.BufferCapacity);
	}

	void INotifyAddedToWorld.AddedToWorld(Actor self)
	{
		this.playerResources = self.Owner.PlayerActor.TraitOrDefault<PlayerResources>();
	}

	void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
	{
		this.playerResources = newOwner.PlayerActor.TraitOrDefault<PlayerResources>();
	}

	protected override bool ActivateInner(Actor self, ResourceCrate crate)
	{
		if (this.processingBuffer.Count >= this.BufferCapacity + 1)
			return false;

		this.processingBuffer.Add(crate);

		self.QueueActivity(new ProcessCrate(crate, this));

		return true;
	}

	protected override bool TryProgress(Actor self, ResourceCrate crate)
	{
		return this.processingBuffer.Count <= this.BufferCapacity;
	}

	protected override void Complete(Actor self, ResourceCrate crate)
	{
		this.OnCrateProcessed();
		crate.Actor.Dispose();
	}

	protected override bool TryAcquireLockInner(Actor clientActor)
	{
		return this.processingBuffer.Count <= this.BufferCapacity + 1 && !this.HasCrate;
	}

	private class ProcessCrate : Activity
	{
		private readonly ResourceCrate resourceCrate;
		private readonly ResourceRefinery resourceRefinery;

		public ProcessCrate(ResourceCrate resourceCrate, ResourceRefinery resourceRefinery)
		{
			this.resourceCrate = resourceCrate;
			this.resourceRefinery = resourceRefinery;
		}

		protected override void OnFirstRun(Actor self)
		{
			var resourceValue = this.resourceRefinery.playerResources?.Info.ResourceValues.FirstOrDefault().Value ?? 1;

			var cash = this.resourceCrate.Resources * resourceValue;

			this.resourceRefinery.playerResources?.GiveCash(cash);

			self.TryGrantingCondition(ref this.resourceRefinery.crateProcessingToken, this.resourceRefinery.info.ProcessCrateCondition);
		}

		public override bool Tick(Actor self)
		{
			if (this.resourceCrate.Actor.Disposed)
			{
				this.QueueChild(new Wait(this.resourceRefinery.info.DelayCrateProcessing));

				return true;
			}

			return false;
		}

		protected override void OnLastRun(Actor self)
		{
			this.resourceRefinery.processingBuffer.Remove(this.resourceCrate);

			if (this.NextActivity is not ProcessCrate)
				self.TryRevokingCondition(ref this.resourceRefinery.crateProcessingToken);
		}
	}
}
