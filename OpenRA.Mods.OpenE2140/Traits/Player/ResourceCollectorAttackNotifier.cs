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
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits
{
	[Desc("Plays an audio notification and shows a radar ping when a miner is attacked.",
		"Attach this to the player actor.")]
	[TraitLocation(SystemActors.Player)]
	public class ResourceCollectorAttackNotifierInfo : TraitInfo
	{
		[Desc("Minimum duration (in milliseconds) between notification events.")]
		public readonly int NotifyInterval = 30000;

		public readonly Color RadarPingColor = Color.Red;

		[Desc("Length of time (in ticks) to display a location ping in the minimap.")]
		public readonly int RadarPingDuration = 250;

		[NotificationReference("Speech")]
		[Desc("The audio notification type to play for undeployed miners.")]
		public string Notification = "ResourceCollectorUnderAttack";

		[FluentReference(optional: true)]
		[Desc("Text notification to display.")]
		public readonly string TextNotification = null;

		public override object Create(ActorInitializer init) { return new ResourceCollectorAttackNotifier(init.Self, this); }
	}

	public class ResourceCollectorAttackNotifier : INotifyDamage
	{
		readonly RadarPings radarPings;
		readonly ResourceCollectorAttackNotifierInfo info;

		long lastAttackTime;

		public ResourceCollectorAttackNotifier(Actor self, ResourceCollectorAttackNotifierInfo info)
		{
			radarPings = self.World.WorldActor.TraitOrDefault<RadarPings>();
			this.info = info;
			lastAttackTime = -info.NotifyInterval;
		}

		void INotifyDamage.Damaged(Actor self, AttackInfo e)
		{
			// Don't track self-damage
			if (e.Attacker != null && e.Attacker.Owner == self.Owner)
				return;

			var notification = string.Empty;
			if (self.Info.HasTraitInfo<DockClientManagerInfo>())
				notification = info.Notification;

			if (string.IsNullOrEmpty(notification))
				return;

			if (Game.RunTime > lastAttackTime + info.NotifyInterval)
			{
				Game.Sound.PlayNotification(self.World.Map.Rules, self.Owner, "Speech", notification, self.Owner.Faction.InternalName);
				TextNotificationsManager.AddTransientLine(self.Owner, info.TextNotification);

				radarPings?.Add(() => self.Owner.IsAlliedWith(self.World.RenderPlayer), self.CenterPosition, info.RadarPingColor, info.RadarPingDuration);

				lastAttackTime = Game.RunTime;
			}
		}
	}
}
