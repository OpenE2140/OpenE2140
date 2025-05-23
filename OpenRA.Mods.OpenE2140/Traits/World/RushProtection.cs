﻿#region Copyright & License Information

/*
 * Copyright (c) The OpenE2140 Developers and Contributors
 * This file is part of OpenE2140, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */

#endregion

using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.OpenE2140.Traits.World;

[TraitLocation(SystemActors.World)]
[Desc("Provides protection to players for a certain time since game start.")]
public class RushProtectionInfo : TraitInfo, ILobbyOptions, IRulesetLoaded
{
	[FluentReference]
	[Desc("Label that will be shown for the rush protection option in the lobby.")]
	public readonly string RushProtectionTimeLabel = "dropdown-rush-protection-time.label";

	[FluentReference]
	[Desc("Tooltip description that will be shown for the rush protection option in the lobby.")]
	public readonly string RushProtectionTimeDescription = "dropdown-rush-protection-time.description";

	[Desc("Rush protection time options that will be shown in the lobby dropdown. Values are in minutes.")]
	public readonly int[] RushProtectionTimeOptions = [0, 1, 2, 5, 7, 10, 15];

	[Desc("Default selection for the rush protection time option in the lobby. Needs to use one of the RushProtectionTimeOptions.")]
	public readonly int RushProtectionTimeDefault;

	[Desc("Prevent the rush protection time option from being changed in the lobby.")]
	public readonly bool RushProtectionTimeLocked;

	[Desc("Whether to display the options dropdown in the lobby.")]
	public readonly bool RushProtectionTimeDropdownVisible = true;

	[Desc("Display order for the rush protection time dropdown in the lobby.")]
	public readonly int RushProtectionTimeDisplayOrder;

	[Desc("Range of protection around each player's home location (spawn point).")]
	public readonly WDist RushProtectionRange = WDist.Zero;

	[Desc("Types of damage to inflict.")]
	public readonly BitSet<DamageType> DamageTypes;

	[Desc("Damage done to enemy units violating the protected area.")]
	public readonly int DamageToViolatingUnits;

	[Desc("Ticks specifying how often the enemy units violating the protected area are damaged.")]
	public readonly int TicksBetweenDamageToViolatingUnits;

	[Desc("ID of the LabelWidget used to display a text ingame that will be updated every second.")]
	public readonly string? CountdownLabel;

	[Desc("List of remaining minutes of protection time when a text and optional speech notification should be made to players.")]
	public readonly Dictionary<int, string?> ProtectionTimeWarnings = new()
	{
		{ 1, null },
		{ 2, null },
		{ 3, null },
		{ 4, null },
		{ 5, null },
		{ 10, null },
	};

	[Desc("Will prevent showing/playing the built-in protection time warnings when set to true.")]
	public readonly bool SkipTimeRemainingNotifications;

	[Desc("Will prevent showing/playing the built-in timer expired notification when set to true.")]
	public readonly bool SkipTimerExpiredNotification;

	[NotificationReference("Speech")]
	[Desc("Speech notification to play when enemy enters protected area.")]
	public readonly string? EnemyAttackingNotification;

	[FluentReference(optional: true)]
	[Desc("Text notification to display when enemy enters protected area.")]
	public readonly string? EnemyAttackingTextNotification;

	[Desc("Minimum duration (in milliseconds) between enemy attacking notification events.")]
	public readonly int EnemyAttackingNotifyInterval = 10000;

	void IRulesetLoaded<ActorInfo>.RulesetLoaded(Ruleset rules, ActorInfo info)
	{
		if (!this.RushProtectionTimeOptions.Contains(this.RushProtectionTimeDefault))
			throw new YamlException("RushProtectionTimeDefault must be a value from RushProtectionTimeOptions");
	}

	[FluentReference]
	private const string NoRushProtection = "options-rush-protection-time.no-limit";

	[FluentReference("minutes")]
	private const string RushProtectionTimeOption = "options-rush-protection-time.options";

	IEnumerable<LobbyOption> ILobbyOptions.LobbyOptions(MapPreview map)
	{
		var timeValues = this.RushProtectionTimeOptions.ToDictionary(m => m.ToStringInvariant(), m =>
		{
			if (m == 0)
				return FluentProvider.GetMessage(NoRushProtection);
			else
				return FluentProvider.GetMessage(RushProtectionTimeOption, "minutes", m);
		});

		yield return new LobbyOption(map, "rushProtectionTime", this.RushProtectionTimeLabel, this.RushProtectionTimeDescription,
			this.RushProtectionTimeDropdownVisible, this.RushProtectionTimeDisplayOrder,
			timeValues, this.RushProtectionTimeDefault.ToStringInvariant(), this.RushProtectionTimeLocked);
	}

	public override object Create(ActorInitializer init)
	{
		return new RushProtection(init.Self, this);
	}
}

public class RushProtection : ITick, IWorldLoaded
{
	[FluentReference]
	private const string RushProtectionCountdown = "label-rush-protection-time-countdown";

	[FluentReference]
	private const string RushProtectionDisabled = "notification-rush-protection-disabled";

	[FluentReference]
	private const string ProtectionTimeWarningNotification = "notification-rush-protection-time-warning";

	private readonly int ticksPerSecond;
	private readonly List<int> proximityTriggers = [];
	private readonly Dictionary<Actor, ActorDamageState> actorDamageStates = [];
	private readonly int protectionTime;

	private LabelWidget? countdownLabel;
	private CachedTransform<int, string>? countdown;
	private int ticksRemaining;
	private long lastNotifyTime;

	public readonly RushProtectionInfo Info;

	public List<ProtectedPlayer> ProtectedPlayers { get; } = [];

	public bool IsEnabled { get; private set; }

	public RushProtection(Actor self, RushProtectionInfo rushProtectionInfo)
	{
		this.Info = rushProtectionInfo;

		this.ticksPerSecond = 1000 / self.World.Timestep;

		var tl = self.World.LobbyInfo.GlobalSettings.OptionOrDefault("rushProtectionTime", this.Info.RushProtectionTimeDefault.ToStringInvariant());
		if (!int.TryParse(tl, out this.protectionTime))
			this.protectionTime = this.Info.RushProtectionTimeDefault;

		// Convert from minutes to ticks
		this.protectionTime *= 60 * this.ticksPerSecond;
	}

	void IWorldLoaded.WorldLoaded(OpenRA.World world, OpenRA.Graphics.WorldRenderer wr)
	{
		if (this.Info.RushProtectionRange <= WDist.Zero || this.Info.DamageToViolatingUnits <= 0
			|| this.Info.TicksBetweenDamageToViolatingUnits <= 0 || this.protectionTime <= 0)
		{
			this.IsEnabled = false;
			return;
		}

		if (!string.IsNullOrWhiteSpace(this.Info.CountdownLabel))
		{
			this.countdownLabel = Ui.Root.GetOrNull<LabelWidget>(this.Info.CountdownLabel);
			if (this.countdownLabel != null)
			{
				this.countdown = new CachedTransform<int, string>(t =>
					FluentProvider.GetMessage(RushProtectionCountdown, "time", WidgetUtils.FormatTime(Math.Max(t - world.Timestep, 0), world.Timestep)));
				this.countdownLabel.GetText = () => this.protectionTime > 0 ? this.countdown.Update(this.ticksRemaining) : "";
			}
		}

		this.InitializeTriggers(world);

		this.ticksRemaining = this.protectionTime;
		this.IsEnabled = true;
	}

	private void InitializeTriggers(OpenRA.World world)
	{
		this.ProtectedPlayers.Clear();

		foreach (var player in world.Players)
		{
			var homeLocation = player.HomeLocation;
			if (homeLocation == CPos.Zero)
				homeLocation = world.Actors.FirstOrDefault(a => a.Owner == player && a.Info.Name == "mpspawn")?.Location ?? CPos.Zero;
			if (homeLocation == CPos.Zero)
				continue;

			var protectedPlayer = new ProtectedPlayer { Player = player, SpawnLocation = homeLocation };
			this.ProtectedPlayers.Add(protectedPlayer);

			var trigger = world.ActorMap.AddProximityTrigger(
				world.Map.CenterOfCell(homeLocation), this.Info.RushProtectionRange, this.Info.RushProtectionRange,
				onEntry: (Actor actor) =>
				{
					// Ignore husks, which are created by vehicles/mechs.
					// Perhaps there's better way to specify the traits to check?
					if (actor.Info.HasTraitInfo<HuskInfo>())
						return;

					if (player.RelationshipWith(actor.Owner) == PlayerRelationship.Enemy)
					{
						this.actorDamageStates[actor] = new ActorDamageState
						{
							DefendingPlayer = player
						};

						// Notify local player about the attack only once. If the enemy actually attacks (e.g. any building),
						// the defending player will be notified about it in separate notification.
						if (actor.World.LocalPlayer == player
							&& Game.RunTime > this.lastNotifyTime + this.Info.EnemyAttackingNotifyInterval)
						{
							PlaySpeechFactionNotification(actor.World, this.Info.EnemyAttackingNotification);
							TextNotificationsManager.AddTransientLine(actor.World.LocalPlayer, this.Info.EnemyAttackingTextNotification);

							this.lastNotifyTime = Game.RunTime;
						}

					}
				},
				onExit: (Actor actor) => this.actorDamageStates.Remove(actor));

			this.proximityTriggers.Add(trigger);
		}
	}

	void ITick.Tick(Actor self)
	{
		if (!this.IsEnabled)
			return;

		this.ticksRemaining = this.protectionTime - self.World.WorldTick;

		if (this.ticksRemaining < 0)
		{
			foreach (var trigger in this.proximityTriggers)
				self.World.ActorMap.RemoveProximityTrigger(trigger);

			this.proximityTriggers.Clear();
			this.IsEnabled = false;

			if (this.countdownLabel != null)
				this.countdownLabel.GetText = () => null;
			this.countdown = null;

			if (!this.Info.SkipTimerExpiredNotification)
				TextNotificationsManager.AddSystemLine(FluentProvider.GetMessage(RushProtectionDisabled));

			return;
		}

		foreach (var (actor, state) in this.actorDamageStates)
		{
			if (--state.TicksToNextDamage >= 0)
				continue;

			// TODO: who should be inflicting damage? World actor? Actor of player being attacked?
			actor.InflictDamage(self.World.WorldActor,
				new Damage(this.Info.DamageToViolatingUnits, this.Info.DamageTypes));

			state.TicksToNextDamage = this.Info.TicksBetweenDamageToViolatingUnits;
		}

		if (this.ticksRemaining < 0 || this.Info.SkipTimeRemainingNotifications)
			return;

		foreach (var (time, notification) in this.Info.ProtectionTimeWarnings)
		{
			if (this.ticksRemaining == time * 60 * this.ticksPerSecond)
			{
				TextNotificationsManager.AddSystemLine(FluentProvider.GetMessage(ProtectionTimeWarningNotification, "minutes", time));

				PlaySpeechFactionNotification(self.World, notification);
			}
		}
	}

	private static void PlaySpeechFactionNotification(OpenRA.World world, string? notification)
	{
		var faction = world.LocalPlayer?.Faction.InternalName;
		Game.Sound.PlayNotification(world.Map.Rules, world.LocalPlayer, "Speech", notification, faction);
	}

	private class ActorDamageState
	{
		public int TicksToNextDamage;

		public required Player DefendingPlayer { get; init; }
	}
}

public class ProtectedPlayer
{
	public required Player Player { get; init; }

	public CPos SpawnLocation { get; set; }
}
