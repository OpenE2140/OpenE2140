using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Buildings;

[Desc("Building can be self-destructed.")]
public class SelfDestructibleInfo : ConditionalTraitInfo, Requires<IHealthInfo>
{
	[Desc("The maximum amount of HP to decrease each step.")]
	public readonly int DamageStep = 7;

	[Desc("Damage types used for the self-destruction.")]
	public readonly BitSet<DamageType> SelfDestructDamageTypes;

	[Desc("Cursor to show when a building can be self-destructed.")]
	[CursorReference]
	public readonly string Cursor = "selfdestruct";

	[Desc("Cursor to display when a building cannot be self-destructed.")]
	[CursorReference]
	public readonly string BlockedCursor = "selfdestruct-blocked";

	[GrantedConditionReference]
	[Desc("The condition to grant to self while the self-destruction is in progress.")]
	public readonly string? SelfDestructCondition;

	[NotificationReference("Speech")]
	[Desc("Voice line to play when self-destruction is started.")]
	public readonly string? SelfDestructingNotification;

	[FluentReference(optional: true)]
	[Desc("Transient text message to display when self-destruction is started.")]
	public readonly string? SelfDestructingTextNotification;

	[NotificationReference("Speech")]
	[Desc("Speech notification to play when the self-destruction is aborted.")]
	public readonly string? SelfDestructingStoppedNotification;

	[Desc("Text notification to display when the self-destruction is aborted.")]
	public readonly string? SelfDestructingStoppedTextNotification;

	public override object Create(ActorInitializer init) { return new SelfDestructible(init.Self, this); }
}

public class SelfDestructible : ConditionalTrait<SelfDestructibleInfo>, ITick, IResolveOrder, INotifyKilled
{
	public const string SelfDestructOrderID = "SelfDestruct";

	private readonly IHealth health;
	private List<INotifySelfDestruction> notifySelfDestruction = [];

	private int condition = Actor.InvalidConditionToken;

	public bool SelfDestructActive { get; private set; }

	public SelfDestructible(Actor self, SelfDestructibleInfo info)
		: base(info)
	{
		this.health = self.Trait<IHealth>();
	}

	protected override void Created(Actor self)
	{
		base.Created(self);

		this.notifySelfDestruction = self.TraitsImplementing<INotifySelfDestruction>().ToList();
	}

	private void UpdateCondition(Actor self)
	{
		if (string.IsNullOrEmpty(this.Info.SelfDestructCondition))
			return;

		self.GrantOrRevokeCondition(ref this.condition, this.SelfDestructActive, this.Info.SelfDestructCondition);
	}

	void ITick.Tick(Actor self)
	{
		if (this.IsTraitDisabled)
		{
			if (this.SelfDestructActive)
				this.StopSelfDestruct(self);

			return;
		}

		if (!this.SelfDestructActive)
			return;

		var damageToInflict = Math.Min(this.Info.DamageStep, this.health.HP);

		self.InflictDamage(self, new Damage(damageToInflict, this.Info.SelfDestructDamageTypes));
	}

	void IResolveOrder.ResolveOrder(Actor self, Order order)
	{
		if (order.OrderString != SelfDestructOrderID)
			return;

		this.ToggleSelfDestructBuilding(self);
	}

	public void ToggleSelfDestructBuilding(Actor self)
	{
		if (this.IsTraitDisabled || self.IsDead)
			return;

		if (this.SelfDestructActive)
		{
			this.StopSelfDestruct(self);

			return;
		}

		this.SelfDestructActive = true;

		Game.Sound.PlayNotification(self.World.Map.Rules, self.Owner, "Speech", this.Info.SelfDestructingNotification, self.Owner.Faction.InternalName);
		TextNotificationsManager.AddTransientLine(self.Owner, this.Info.SelfDestructingTextNotification);

		this.UpdateCondition(self);

		this.notifySelfDestruction.ForEach(n => n.SelfDestructionStarted(self));
	}

	private void StopSelfDestruct(Actor self)
	{
		this.SelfDestructActive = false;
		this.UpdateCondition(self);

		Game.Sound.PlayNotification(self.World.Map.Rules, self.Owner, "Speech", this.Info.SelfDestructingStoppedNotification, self.Owner.Faction.InternalName);
		TextNotificationsManager.AddTransientLine(self.Owner, this.Info.SelfDestructingStoppedTextNotification);

		this.notifySelfDestruction.ForEach(n => n.SelfDestructionAborted(self));
	}

	void INotifyKilled.Killed(Actor self, AttackInfo e)
	{
		this.SelfDestructActive = false;
		this.UpdateCondition(self);
	}
}
