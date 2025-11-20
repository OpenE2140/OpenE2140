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

using OpenRA.Activities;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Activites;

public class BuildWall : Activity
{
	private enum BuildState { None, MovingToTarget, Building }

	private readonly CPos targetLocation;
	private readonly WallBuilder wallBuilder;
	private readonly Mobile mobile;
	private readonly AmmoPool[] ammoPools;

	private BuildState state = BuildState.None;

	public BuildWall(Actor self, CPos targetLocation)
	{
		this.targetLocation = targetLocation;
		this.wallBuilder = self.Trait<WallBuilder>();
		this.mobile = self.Trait<Mobile>();
		this.ammoPools = self.TraitsImplementing<AmmoPool>().Where(p => p.Info.Name == this.wallBuilder.Info.AmmoPoolName).ToArray();
	}

	public override bool Tick(Actor self)
	{
		if (this.IsCanceling)
		{
			if (this.state == BuildState.Building)
				foreach (var t in self.TraitsImplementing<INotifyWallBuilding>())
					t.WallBuildingCanceled(self, this.targetLocation);

			return true;
		}

		switch (this.state)
		{
			case BuildState.None:
			{
				if (self.Location != this.targetLocation)
					this.QueueChild(this.mobile.MoveTo(this.targetLocation));

				this.state = BuildState.MovingToTarget;
				break;
			}
			case BuildState.MovingToTarget:
			{
				if (self.Location != this.targetLocation)
				{
					// Skip placing this wall, if:
					// - there already is a wall at target location.
					// - the wall builder cannot enter target cell.
					// But only if the wall builder can see it.
					if (!self.World.FogObscures(this.targetLocation)
						&& (!this.mobile.CanEnterCell(this.targetLocation, self, BlockedByActor.Immovable)
							|| self.World.ActorMap.AnyActorsAt(this.targetLocation, SubCell.Any, a => this.wallBuilder.Info.Wall == a.Info.Name)))
						return true;

					this.QueueChild(this.mobile.MoveTo(this.targetLocation));

					return false;
				}

				if (self.World.ActorMap.AnyActorsAt(this.targetLocation, SubCell.FullCell, a => a != self))
				{
					return true;
				}

				// If the wall builder uses any ammo pool, check if there's enough ammo to build a wall.
				if (this.ammoPools != null)
				{
					var pool = this.ammoPools.FirstOrDefault();
					if (pool == null)
						return false;

					if (pool.CurrentAmmoCount < this.wallBuilder.Info.AmmoUsage)
						return false;
				}

				this.state = BuildState.Building;
				this.QueueChild(new Wait(this.wallBuilder.Info.PreBuildDelay));

				foreach (var t in self.TraitsImplementing<INotifyWallBuilding>())
					t.WallBuilding(self, this.targetLocation);

				break;
			}
			case BuildState.Building:
			{
				if (self.World.ActorMap.AnyActorsAt(this.targetLocation, SubCell.FullCell, a => a != self))
				{
					this.Cancel(self, true);
					return true;
				}

				// If the wall builder uses any ammo pool, take ammo from any matching pool.
				if (this.ammoPools != null)
				{
					var pool = this.ammoPools.FirstOrDefault();
					if (pool == null)
						return false;

					if (!pool.TakeAmmo(self, this.wallBuilder.Info.AmmoUsage))
						return false;
				}

				// Currently, the wall is build by the trait *after* the wall builder leaves target cell.
				foreach (var t in self.TraitsImplementing<INotifyWallBuilding>())
					t.WallBuildingCompleted(self, this.targetLocation);

				return true;
			}
			default:
				break;
		}
		return false;
	}

	public override IEnumerable<TargetLineNode> TargetLineNodes(Actor self)
	{
		yield return new TargetLineNode(Target.FromCell(self.World, this.targetLocation), this.wallBuilder.Info.TargetLineColor);
	}

	public override IEnumerable<Target> GetTargets(Actor self)
	{
		yield return Target.FromCell(self.World, this.targetLocation);
	}
}
