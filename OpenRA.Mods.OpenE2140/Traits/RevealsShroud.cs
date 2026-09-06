using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits;

[Desc($"Custom version of {nameof(OpenRA.Mods.Common.Traits.RevealsShroud)}, which expands range of {nameof(AttachedCargo)} actor " +
	$"with passengers that have larger radius for revealing shroud.")]
public class RevealsShroudInfo : OpenRA.Mods.Common.Traits.RevealsShroudInfo
{
	public override object Create(ActorInitializer init) { return new RevealsShroud(this); }
}

public class RevealsShroud : OpenRA.Mods.Common.Traits.RevealsShroud, INotifyPassengerEntered, INotifyPassengerExited
{
	private IEnumerable<int> rangeModifiers = [];
	private AttachedCargo? attachedCargo;
	private int passengerMaxRevealRange;

	public RevealsShroud(RevealsShroudInfo info)
		: base(info)
	{
	}

	protected override void Created(Actor self)
	{
		base.Created(self);

		this.rangeModifiers = self.TraitsImplementing<IRevealsShroudModifier>().ToArray().Select(x => x.GetRevealsShroudModifier());
		this.attachedCargo = self.TraitOrDefault<AttachedCargo>();
	}

	void INotifyPassengerEntered.OnPassengerEntered(Actor self, Actor passenger)
	{
		var maxRange = passenger.TraitsImplementing<AffectsShroud>().Max(a => a.Range.Length);
		if (this.attachedCargo != null)
		{
			maxRange = Math.Max(maxRange, this.GetPassengerMaxRevealRange());
		}
		this.passengerMaxRevealRange = maxRange;
	}

	void INotifyPassengerExited.OnPassengerExited(Actor self, Actor passenger)
	{
		this.passengerMaxRevealRange = this.GetPassengerMaxRevealRange();
	}

	public override WDist Range
	{
		get
		{
			if (this.CachedTraitDisabled)
				return WDist.Zero;

			var ownRange = Util.ApplyPercentageModifiers(this.Info.Range.Length, this.rangeModifiers);
			if (this.attachedCargo == null || this.attachedCargo.PassengerCount == 0)
			{
				return new WDist(ownRange);
			}

			return new WDist(Math.Max(ownRange, this.passengerMaxRevealRange));
		}
	}

	private int GetPassengerMaxRevealRange()
	{
		if (this.attachedCargo == null || this.attachedCargo.PassengerCount == 0)
			return 0;

		var maxRange = 0;
		foreach (var passenger in this.attachedCargo.Passengers.Where(p => !p.Disposed))
		{
			foreach (var affectsShroud in passenger.TraitsImplementing<AffectsShroud>())
			{
				if (!affectsShroud.IsTraitDisabled)
					maxRange = Math.Max(maxRange, affectsShroud.Range.Length);
			}
		}

		return maxRange;
	}
}
