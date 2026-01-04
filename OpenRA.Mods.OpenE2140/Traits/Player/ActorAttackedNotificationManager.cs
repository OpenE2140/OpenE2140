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

using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Traits.Misc;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits;

[Desc("Plays an audio notification and shows a radar ping when a actor is attacked.",
	"Attach this to the player actor.",
	"Actors")]
public class ActorAttackedNotificationManagerInfo : TraitInfo
{
	[Desc("Minimum duration (in milliseconds) between notification events of same type.")]
	public readonly Dictionary<string, int> NotifyIntervals = [];

	public override object Create(ActorInitializer init) { return new ActorAttackedNotificationManager(init.Self, this); }
}

public class ActorAttackedNotificationManager
{
	private readonly RadarPings radarPings;
	private readonly ActorAttackedNotificationManagerInfo info;

	private readonly Dictionary<string, long> lastAttackTimes = [];

	public ActorAttackedNotificationManager(Actor self, ActorAttackedNotificationManagerInfo info)
	{
		this.radarPings = self.World.WorldActor.TraitOrDefault<RadarPings>();
		this.info = info;
	}

	internal void OnActorAttacked(Actor actor, ActorAttackedNotificationInfo info)
	{
		if (!actor.Owner.IsAlliedWith(actor.World.RenderPlayer))
			return;

		var type = info.NotificationType ?? "";
		var notifyInterval = this.info.NotifyIntervals.GetValueOrDefault(type, 10_000);
		var lastAttackTime = this.lastAttackTimes.GetOrAdd(type, -notifyInterval);

		if (Game.RunTime > lastAttackTime + notifyInterval)
		{
			Game.Sound.PlayNotification(actor.World.Map.Rules, actor.Owner, "Speech", info.Notification, actor.Owner.Faction.InternalName);
			TextNotificationsManager.AddTransientLine(actor.Owner, info.TextNotification);

			this.radarPings?.Add(() => true, actor.CenterPosition, info.RadarPingColor, info.RadarPingDuration);

			this.lastAttackTimes[type] = Game.RunTime;
		}
	}
}
