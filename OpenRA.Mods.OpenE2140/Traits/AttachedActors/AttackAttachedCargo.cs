using OpenRA.Activities;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.AttachedActors;

public class AttackAttachedCargoInfo : AttackFollowInfo
{
	public override object Create(ActorInitializer init)
	{
		return new AttackAttachedCargo(init.Self, this);
	}
}


public class AttackAttachedCargo : AttackFollow, INotifyPassengerEntered, INotifyPassengerExited, INotifyStanceChanged
{
	private readonly List<Armament> armaments = [];
	private readonly Dictionary<Actor, AttackBase> paxAttackBases = [];
	private readonly Dictionary<Actor, AutoTarget> paxAutoTargets = [];
	private UnitStance? oldUnitStance;
	private AutoTarget? autoTarget;

	private INotifyAttack[] notifyAttacks = [];

	public AttackAttachedCargo(Actor self, AttackFollowInfo info)
		: base(self, info)
	{
	}

	protected override void Created(Actor self)
	{
		this.notifyAttacks = self.TraitsImplementing<INotifyAttack>().ToArray();
		this.autoTarget = self.TraitOrDefault<AutoTarget>();
		this.oldUnitStance = this.autoTarget?.Stance;
		base.Created(self);
	}

	protected override Func<IEnumerable<Armament>> InitializeGetArmaments(Actor self)
	{
		return () => this.armaments;
	}

	void INotifyPassengerEntered.OnPassengerEntered(Actor self, Actor passenger)
	{
		// TODO: multiple AttackBases?
		var ab = passenger.TraitsImplementing<AttackBase>().FirstOrDefault();
		if (ab != null)
			this.paxAttackBases[passenger] = ab;

		var at = passenger.TraitsImplementing<AutoTarget>().FirstOrDefault();
		if (at != null)
			this.paxAutoTargets[passenger] = at;

		foreach (var a in passenger.TraitsImplementing<Armament>())
		{
			if (this.Info.Armaments.Contains(a.Info.Name))
			{
				a.AddNotifyAttacks(self, this.notifyAttacks);
				this.armaments.Add(a);
			}
		}
	}

	void INotifyPassengerExited.OnPassengerExited(Actor self, Actor passenger)
	{
		// TODO: multiple AttackBases?
		this.paxAttackBases.Remove(passenger);

		this.paxAutoTargets.Remove(passenger);

		foreach (var a in this.armaments.ToList())
		{
			if (a.Actor == passenger)
			{
				a.RemoveNotifyAttacks(this.notifyAttacks);
				this.armaments.Remove(a);
			}
		}
	}

	protected override bool CanAttack(Actor self, in Target target)
	{
		if (target.Type == TargetType.Invalid)
			return false;

		// TODO: AttackFrontals need to face target, transporter needs to turn to it to be able to attack it.

		return base.CanAttack(self, target);
	}

	public override void DoAttack(Actor self, in Target target)
	{
		if (!this.CanAttack(self, target))
		{
			return;
		}

		foreach (var (actor, ab) in this.paxAttackBases)
		{
			if (ab is AttackFrontal)
			{
				ab.DoAttack(actor, target);
			}
		}
	}

	public override void OnStopOrder(Actor self)
	{
		base.OnStopOrder(self);

		foreach (var (actor, ab) in this.paxAttackBases)
		{
			if (ab is AttackFollow attackFollow)
			{
				attackFollow.OnStopOrder(actor);
			}
		}
	}

	protected override void Tick(Actor self)
	{
		base.Tick(self);

		if (this.IsTraitDisabled || this.IsTraitPaused)
			return;

		if (this.autoTarget != null && this.oldUnitStance != null && this.oldUnitStance != this.autoTarget.Stance)
		{
			foreach (var (actor, at) in this.paxAutoTargets)
			{
				at.SetStance(actor, this.autoTarget.Stance);
			}

			this.oldUnitStance = this.autoTarget.Stance;
		}
	}

	public override Activity GetAttackActivity(
		Actor self, AttackSource source, in Target newTarget, bool allowMove, bool forceAttack, Color? targetLineColor = null)
	{
		var parentActivity = new AttackFollowAttackActivity(this.paxAttackBases, newTarget, forceAttack);

		return parentActivity.WithChild(base.GetAttackActivity(self, source, newTarget, allowMove, forceAttack, targetLineColor));
	}

	private class AttackFollowAttackActivity : Activity
	{
		private readonly Dictionary<Actor, AttackBase> paxAttackBases;
		private readonly bool forceAttack;
		private readonly Target target;

		public AttackFollowAttackActivity(Dictionary<Actor, AttackBase> paxAttackBases, in Target target, bool forceAttack)
		{
			this.paxAttackBases = paxAttackBases;
			this.target = target;
			this.forceAttack = forceAttack;
		}

		protected override void OnFirstRun(Actor self)
		{
			if (!this.target.IsValidFor(self))
			{
				this.Cancel(self);
				return;
			}

			this.UpdateRequestedTarget();
		}

		private void UpdateRequestedTarget()
		{
			foreach (var ab in this.paxAttackBases.Values)
			{
				if (ab is AttackFollow attackFollow && attackFollow.RequestedTarget != this.target)
				{
					attackFollow.SetRequestedTarget(this.target, this.forceAttack);
				}
			}
		}

		protected override void OnLastRun(Actor self)
		{
			foreach (var ab in this.paxAttackBases.Values)
			{
				if (ab is AttackFollow attackFollow)
				{
					attackFollow.ClearRequestedTarget();
				}
			}
		}

		public override IEnumerable<TargetLineNode> TargetLineNodes(Actor self)
		{
			return this.ChildActivity?.TargetLineNodes(self) ?? [];
		}
	}
}
