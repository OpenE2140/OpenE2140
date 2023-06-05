using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.OpenE2140.Traits;

[Desc("Makes actor targetability dependent on viewer's warheads")]
public class WarheadDependentTargetableInfo : TargetableInfo
{
	[Desc("List of viewer warheads that makes it not able to target this actor.")]
	public readonly string[] InvalidViewerWarheads = Array.Empty<string>();

	public override object Create(ActorInitializer init) { return new WarheadDependentTargetable(this); }
}

public class WarheadDependentTargetable : Targetable
{
	private readonly WarheadDependentTargetableInfo info;

	public WarheadDependentTargetable(WarheadDependentTargetableInfo info)
		: base(info)
	{
		this.info = info;
	}

	public override bool TargetableBy(Actor self, Actor viewer)
	{
		if (this.IsTraitDisabled)
			return false;

		// Actor is targetable by the viewer only if any armament has weapon with invalid warhead
		var invalidArmaments = viewer.Info.TraitInfos<ArmamentInfo>()
			.Where(a => a.WeaponInfo.Warheads.Any(a => this.info.InvalidViewerWarheads.Any(w => a.GetType().Name.StartsWith(w))) == true);
		if (invalidArmaments.Any())
			return false;

		return base.TargetableBy(self, viewer);
	}
}
