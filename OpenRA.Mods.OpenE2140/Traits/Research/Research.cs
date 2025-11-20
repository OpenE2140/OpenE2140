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

namespace OpenRA.Mods.OpenE2140.Traits;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("Add this to a player, to enable the research system.")]
public class ResearchInfo : TraitInfo, Requires<PlayerResourcesInfo>, Requires<TechTreeInfo>
{
	[Desc("Percentage of research lost when aborting a research.")]
	public readonly int AbortPenalty = 100;

	[NotificationReference("Speech")]
	public readonly string ResearchStartedSpeechNotification = "ResearchStarted";

	[NotificationReference("Speech")]
	public readonly string ResearchCompleteSpeechNotification = "ResearchComplete";

	[NotificationReference("Speech")]
	public readonly string ResearchAbortedSpeechNotification = "ResearchAborted";

	[NotificationReference("Speech")]
	public readonly string ResearchAquiredSpeechNotification = "ResearchAquired";

	public override object Create(ActorInitializer init)
	{
		return new Research(this, init.Self);
	}
}

public class Research : INotifyAddedToWorld, IResolveOrder, ITechTreePrerequisite
{
	public const string StartResearchOrder = "StartResearch";
	public const string StopResearchOrder = "StopResearch";

	private readonly ResearchInfo info;
	private readonly Actor self;
	private readonly Player player;
	private readonly PlayerResources playerResources;
	private readonly TechTree techTree;
	private ResearchLimit? researchLimit;
	private DeveloperMode? developerMode;
	private readonly List<Researchable> researchables = [];

	public Researchable? Current { get; private set; }

	public IEnumerable<string> ProvidesPrerequisites =>
		this.researchables.Where(researchable => researchable.RemainingDuration == 0).Select(researchable => researchable.Info.Id);

	public Research(ResearchInfo info, Actor self)
	{
		this.info = info;
		this.self = self;
		this.player = self.Owner;
		this.playerResources = self.Trait<PlayerResources>();
		this.techTree = self.Trait<TechTree>();
	}

	void INotifyAddedToWorld.AddedToWorld(Actor self)
	{
		this.researchLimit = self.World.WorldActor.TraitOrDefault<ResearchLimit>();
		this.developerMode = self.TraitOrDefault<DeveloperMode>();
		this.researchables.AddRange(self.TraitsImplementing<Researchable>());
	}

	public void ConquerResearch(Player oldOwner)
	{
		var oldResearch = oldOwner.PlayerActor.Trait<Research>();

		foreach (var researchable in this.researchables)
		{
			// Check whether this could be researched.
			if (researchable.RemainingDuration == 0 || this.GetMissingRequirements(researchable).Any())
				continue;

			// Check if the old owner had this research researched.
			if (oldResearch.researchables.FirstOrDefault(e => e.Info.Id == researchable.Info.Id) is not { RemainingDuration: 0 })
				continue;

			// If it was the current research, avoid announcing research completed.
			if (this.Current == researchable)
				this.Current = null;

			// Research it.
			researchable.RemainingDuration = 0;
			researchable.RemainingCost = 0;
			this.techTree.Update();

			Game.Sound.PlayNotification(
				this.player.World.Map.Rules,
				this.player,
				"Speech",
				this.info.ResearchAquiredSpeechNotification,
				this.player.Faction.InternalName
			);

			break;
		}
	}

	public void DoResearch()
	{
		if (this.Current == null)
			return;

		var progress = this.developerMode is { FastBuild: true } ? this.Current.RemainingDuration : 1;

		var expectedRemainingDuration = this.Current.RemainingDuration - progress;
		var expectedRemainingCost = this.Current.Info.Cost * expectedRemainingDuration / Math.Max(this.Current.Info.Duration, 1);

		var cost = this.Current.RemainingCost - expectedRemainingCost;

		if (!this.playerResources.TakeCash(cost))
			return;

		this.Current.RemainingDuration -= progress;
		this.Current.RemainingCost -= cost;

		if (this.Current.RemainingDuration != 0)
			return;

		this.Current = null;
		this.techTree.Update();

		Game.Sound.PlayNotification(
			this.player.World.Map.Rules,
			this.player,
			"Speech",
			this.info.ResearchCompleteSpeechNotification,
			this.player.Faction.InternalName
		);
	}

	void IResolveOrder.ResolveOrder(Actor self, Order order)
	{
		switch (order.OrderString)
		{
			case Research.StartResearchOrder:
			{
				this.Start(order.TargetString);

				break;
			}

			case Research.StopResearchOrder:
			{
				this.Stop(order.TargetString);

				break;
			}
		}
	}

	private void Start(string research)
	{
		var researchable = this.researchables.FirstOrDefault(e => e.Info.Id == research);

		// Check whether this could be researched.
		if (researchable == null || researchable.RemainingDuration == 0 || this.GetMissingRequirements(researchable).Any())
			return;

		if (this.researchLimit != null && this.developerMode is not { AllTech: true } && researchable.Info.Level > this.researchLimit.Limit)
			return;

		// Check for faction restrictions.
		if (this.developerMode is not { AllTech: true }
			&& researchable.Info.Factions.Length > 0
			&& !researchable.Info.Factions.Contains(this.player.Faction.InternalName))
			return;

		// If it was the current research, avoid announcing research started.
		if (this.Current == researchable)
			return;

		// Apply abort penalty.
		this.ApplyAbortPenalty();

		// Start the research.
		this.Current = researchable;

		Game.Sound.PlayNotification(
			this.player.World.Map.Rules,
			this.player,
			"Speech",
			this.info.ResearchStartedSpeechNotification,
			this.player.Faction.InternalName
		);
	}

	private void Stop(string research)
	{
		// Check whether we are researching this research.
		if (this.Current == null || this.Current.Info.Id != research)
			return;

		// Apply abort penalty.
		this.ApplyAbortPenalty();

		// Stop the research.
		this.Current = null;

		Game.Sound.PlayNotification(
			this.player.World.Map.Rules,
			this.player,
			"Speech",
			this.info.ResearchAbortedSpeechNotification,
			this.player.Faction.InternalName
		);
	}

	private void ApplyAbortPenalty()
	{
		if (this.Current == null)
			return;

		this.Current.RemainingDuration += (this.Current.PenaltySafeDuration - this.Current.RemainingDuration) * this.info.AbortPenalty / 100;
		this.Current.RemainingCost += (this.Current.PenaltySafeCost - this.Current.RemainingCost) * this.info.AbortPenalty / 100;

		this.Current.PenaltySafeDuration = this.Current.RemainingDuration;
		this.Current.PenaltySafeCost = this.Current.RemainingCost;
	}

	public IEnumerable<Researchable> GetMissingRequirements(Researchable researchable)
	{
		var wasFound = false;

		foreach (var other in this.researchables)
		{
			if (other == researchable)
				wasFound = true;

			if (other.Info.Factions.Length > 0 && !other.Info.Factions.Contains(this.self.Owner.Faction.InternalName))
				continue;

			if (other.RemainingDuration == 0)
				continue;

			if (other.Info.Level < researchable.Info.Level)
				yield return other;

			if (other.Info.Level == researchable.Info.Level && !wasFound)
				yield return other;
		}
	}

	public void HideUnbuildableActors(Actor self)
	{
		if (this.researchLimit == null)
			return;

		var techTree = self.Owner.PlayerActor.Trait<TechTree>();
		var queues = self.TraitsImplementing<ProductionQueue>();
		var shouldUpdateTechTree = false;
		foreach (var queue in queues)
		{
			if (!queue.Enabled)
				return;

			var producibles = AllBuildables(self, queue.Info.Type);

			foreach (var actorInfo in producibles)
			{
				var buildable = actorInfo.TraitInfo<BuildableInfo>();
				var researchTechs = buildable.Prerequisites
					.Select(p => this.researchables.FirstOrDefault(r => r.Info.Id == p))
					.Where(r => r != null && r.Info.Level > this.researchLimit.Limit).ToArray();

				if (researchTechs.Length > 0)
				{
					shouldUpdateTechTree = true;

					// Hiding works by adding prerequisite that will cause actor being hidden in ProductionPaletteWidget.
					// Enabling All Tech dev command will continue to work as expected, because if it's enabled, all prerequisites are ignored.
					techTree.Add(actorInfo.Name, ["~disabled"], 0, queue);
				}
			}
		}

		if (shouldUpdateTechTree)
			techTree.Update();

		IEnumerable<ActorInfo> AllBuildables(Actor self, string category)
		{
			return self.World.Map.Rules.Actors.Values
				.Where(x =>
					x.Name[0] != '^' &&
					x.HasTraitInfo<BuildableInfo>() &&
					x.TraitInfo<BuildableInfo>().Queue.Contains(category));
		}
	}
}
