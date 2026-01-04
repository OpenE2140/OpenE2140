using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Misc;

public class ActorAttackedNotificationInfo : TraitInfo, IRulesetLoaded
{
	[Desc("Type under which notifications are grouped (e.g. group of actor types) and use same notification interval.")]
	public readonly string? NotificationType;

	public readonly Color RadarPingColor = Color.Red;

	[Desc("Length of time (in ticks) to display a location ping in the minimap.")]
	public readonly int RadarPingDuration = 250;

	[NotificationReference("Speech")]
	[Desc("The audio notification type to play.")]
	public string? Notification;

	[FluentReference(optional: true)]
	[Desc("Text notification to display.")]
	public readonly string? TextNotification;

	public override object Create(ActorInitializer init) { return new ActorAttackedNotification(this); }

	public void RulesetLoaded(Ruleset rules, ActorInfo info)
	{
		if (!rules.Actors[SystemActors.Player].HasTraitInfo<ActorAttackedNotificationManagerInfo>())
		{
			throw new YamlException(
				$"{nameof(ActorAttackedNotification)} requires that {nameof(ActorAttackedNotificationManager)} is defined on Player actor");
		}
	}
}

public class ActorAttackedNotification : INotifyCreated, INotifyDamage
{
	private readonly ActorAttackedNotificationInfo info;
	private ActorAttackedNotificationManager? manager;

	public ActorAttackedNotification(ActorAttackedNotificationInfo actorAttackedNotificationInfo)
	{
		this.info = actorAttackedNotificationInfo;
	}

	void INotifyCreated.Created(Actor self)
	{
		this.manager = self.Owner.PlayerActor.Trait<ActorAttackedNotificationManager>();
	}

	void INotifyDamage.Damaged(Actor self, AttackInfo e)
	{
		if (this.manager == null)
			return;

		if (self.World.LocalPlayer == null || self.World.LocalPlayer.Spectating)
			return;

		if (e.Attacker == null)
			return;

		if (e.Attacker.Owner == self.Owner)
			return;

		if (e.Attacker == self.World.WorldActor)
			return;

		if (e.Attacker.Owner.IsAlliedWith(self.Owner) && e.Damage.Value <= 0)
			return;

		this.manager.OnActorAttacked(self, this.info);
	}
}
