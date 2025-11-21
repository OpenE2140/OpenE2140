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

namespace OpenRA.Mods.OpenE2140.Traits;

public class BuildingConqueredNotificationInfo : ConditionalTraitInfo
{
	[NotificationReference("Speech")]
	[Desc("Speech notification to play to the new owner.")]
	public readonly string Notification = "BuildingConquered";

	[FluentReference(optional: true)]
	[Desc("Text notification to display to the new owner.")]
	public readonly string? TextNotification;

	[Desc("Specifies if Notification is played with the voice of the new owners faction.")]
	public readonly bool NewOwnerVoice = true;

	[NotificationReference("Speech")]
	[Desc("Speech notification to play to the old owner.")]
	public readonly string? LoseNotification;

	[FluentReference(optional: true)]
	[Desc("Text notification to display to the old owner.")]
	public readonly string? LoseTextNotification;

	[Desc("Specifies if LoseNotification is played with the voice of the new owners faction.")]
	public readonly bool LoseNewOwnerVoice = false;

	public override object Create(ActorInitializer init) { return new BuildingConqueredNotification(this); }
}

public class BuildingConqueredNotification : ConditionalTrait<BuildingConqueredNotificationInfo>, INotifyBuildingConquered
{
	public BuildingConqueredNotification(BuildingConqueredNotificationInfo info)
		: base(info)
	{
	}

	void INotifyBuildingConquered.OnConquering(Actor self, Actor conqueror, Player oldOwner, Player newOwner)
	{
		var faction = this.Info.NewOwnerVoice ? newOwner.Faction.InternalName : oldOwner.Faction.InternalName;
		Game.Sound.PlayNotification(self.World.Map.Rules, newOwner, "Speech", this.Info.Notification, faction);
		TextNotificationsManager.AddTransientLine(newOwner, this.Info.TextNotification);

		var loseFaction = this.Info.LoseNewOwnerVoice ? newOwner.Faction.InternalName : oldOwner.Faction.InternalName;
		Game.Sound.PlayNotification(self.World.Map.Rules, oldOwner, "Speech", this.Info.LoseNotification, loseFaction);
		TextNotificationsManager.AddTransientLine(oldOwner, this.Info.LoseTextNotification);
	}
}
