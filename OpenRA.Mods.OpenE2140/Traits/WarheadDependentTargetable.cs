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

namespace OpenRA.Mods.OpenE2140.Traits
{
	[Desc("Makes actor targetability dependent on viewer's warheads")]
	public class WarheadDependentTargetableInfo : TargetableInfo
	{
		[Desc("List of viewer warheads that makes it not able to target this actor.")]
		public readonly string[] InvalidViewerWarheads = [];

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
				.Where(a => a.WeaponInfo.Warheads.Any(a => this.info.InvalidViewerWarheads.Any(w => a.GetType().Name.StartsWith(w))));

			if (invalidArmaments.Any())
				return false;

			return base.TargetableBy(self, viewer);
		}
	}
}

