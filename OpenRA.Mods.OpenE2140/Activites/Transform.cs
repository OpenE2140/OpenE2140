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
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.OpenE2140.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;
using INotifyTransform = OpenRA.Mods.OpenE2140.Traits.INotifyTransform;

namespace OpenRA.Mods.OpenE2140.Activites;

public class Transform : Activity
{
	public readonly string ToActor;
	public CVec Offset = CVec.Zero;
	public WAngle Facing = new(384);
	public string[] Sounds = [];
	public string? Notification;
	public string? TextNotification;
	public int ForceHealthPercentage = 0;
	public bool SkipMakeAnims = false;
	public string? Faction;

	public Transform(string toActor)
	{
		this.ToActor = toActor;
	}

	protected override void OnFirstRun(Actor self)
	{
		if (self.Info.HasTraitInfo<IFacingInfo>())
			this.QueueChild(new Turn(self, this.Facing));

		if (self.Info.HasTraitInfo<AircraftInfo>())
			this.QueueChild(new Land(self));
	}

	public override bool Tick(Actor self)
	{
		if (this.IsCanceling)
			return true;

		// Prevent deployment in bogus locations
		var transforms = self.TraitOrDefault<ITransforms>();
		if (transforms != null && !transforms.CanDeploy(self))
		{
			foreach (var nt in self.TraitsImplementing<INotifyTransform>())
				nt.TransformCanceled(self);
			return true;
		}

		foreach (var nt in self.TraitsImplementing<INotifyTransform>())
			nt.BeforeTransform(self);

		var makeAnimation = self.TraitOrDefault<WithMakeAnimation>();
		if (!this.SkipMakeAnims && makeAnimation != null)
		{
			// Once the make animation starts the activity must not be stopped anymore.
			this.IsInterruptible = false;

			// Wait forever
			this.QueueChild(new WaitFor(() => false));
			makeAnimation.Reverse(self, () => this.DoTransform(self, transforms, makeAnimation));
			return false;
		}

		this.DoTransform(self, transforms, null);
		return true;
	}

	private void DoTransform(Actor self, ITransforms? transforms, WithMakeAnimation? makeAnimation)
	{
		// This activity may be buried as a child within one or more parents
		// We need to consider the top-level activities when transferring orders to the new actor!
		var currentActivity = self.CurrentActivity;

		self.World.AddFrameEndTask(w =>
		{
			if (self.IsDead || self.WillDispose)
				return;

			// Prevent deployment in bogus locations
			if (transforms != null && !transforms.CanDeploy(self))
			{
				if (!this.SkipMakeAnims && makeAnimation != null)
					makeAnimation.Forward(self, () => { this.IsInterruptible = true; this.Cancel(self, true); });
				else
				{
					this.IsInterruptible = true;
					this.Cancel(self, true);
				}

				foreach (var nt in self.TraitsImplementing<INotifyTransform>())
					nt.TransformCanceled(self);

				return;
			}

			foreach (var nt in self.TraitsImplementing<INotifyTransform>())
				nt.OnTransform(self);

			var selected = w.Selection.Contains(self);
			var controlgroup = w.ControlGroups.GetControlGroupForActor(self);

			self.Dispose();
			foreach (var s in this.Sounds)
				Game.Sound.PlayToPlayer(SoundType.World, self.Owner, s, self.CenterPosition);

			Game.Sound.PlayNotification(self.World.Map.Rules, self.Owner, "Speech", this.Notification, self.Owner.Faction.InternalName);
			TextNotificationsManager.AddTransientLine(self.Owner, this.TextNotification);

			var init = new TypeDictionary
			{
				new LocationInit(self.Location + this.Offset),
				new OwnerInit(self.Owner),
				new FacingInit(this.Facing),
			};

			if (this.SkipMakeAnims)
				init.Add(new SkipMakeAnimsInit());

			if (this.Faction != null)
				init.Add(new FactionInit(this.Faction));

			var health = self.TraitOrDefault<IHealth>();
			if (health != null)
			{
				// Cast to long to avoid overflow when multiplying by the health
				var newHP = this.ForceHealthPercentage > 0 ? this.ForceHealthPercentage : (int)(health.HP * 100L / health.MaxHP);
				init.Add(new HealthInit(newHP));
			}

			foreach (var modifier in self.TraitsImplementing<ITransformActorInitModifier>())
				modifier.ModifyTransformActorInit(self, init);

			var a = w.CreateActor(this.ToActor, init);
			foreach (var nt in self.TraitsImplementing<INotifyTransform>())
				nt.AfterTransform(a);

			// Use self.CurrentActivity to capture the parent activity if Transform is a child
			foreach (var transfer in currentActivity.ActivitiesImplementing<IssueOrderAfterTransform>(false))
			{
				if (transfer.IsCanceling)
					continue;

				var order = transfer.IssueOrderForTransformedActor(a);
				foreach (var t in a.TraitsImplementing<IResolveOrder>())
					t.ResolveOrder(a, order);
			}

			self.ReplacedByActor = a;

			if (selected)
				w.Selection.Add(a);

			if (controlgroup.HasValue)
				w.ControlGroups.AddToControlGroup(a, controlgroup.Value);
		});
	}

	private sealed class IssueOrderAfterTransform : Activity
	{
		private readonly string orderString;
		private readonly Target target;
		private readonly Color? targetLineColor;

		public IssueOrderAfterTransform(string orderString, in Target target, Color? targetLineColor = null)
		{
			this.orderString = orderString;
			this.target = target;
			this.targetLineColor = targetLineColor;
		}

		public Order IssueOrderForTransformedActor(Actor newActor)
		{
			return new Order(this.orderString, newActor, this.target, true);
		}

		public override IEnumerable<TargetLineNode> TargetLineNodes(Actor self)
		{
			if (this.targetLineColor != null)
				yield return new TargetLineNode(this.target, this.targetLineColor.Value);
		}
	}
}
