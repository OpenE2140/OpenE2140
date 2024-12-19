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
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.OpenE2140.Extensions;

namespace OpenRA.Mods.OpenE2140.Traits.Resources.Activities;

public class ResourceCrateMovementActivity : Activity
{
	private enum DockState
	{
		None,
		Docking,
		Undocking,
		Complete
	}

	private readonly WithSpriteBody wsb;
	private readonly CrateTransporter crateTransporter;
	private readonly bool isLoading;
	private readonly Action continuationCallback;
	private readonly CrateMoveSequence crateMoveSequence;

	private int currentIndex;
	private int moveTick;

	private DockState state = DockState.None;

	private string DockSequence => this.crateTransporter.Info.DockSequence;
	private string DockLoopSequence => this.crateTransporter.Info.DockLoopSequence;

	public ResourceCrateMovementActivity(
		Actor self,
		bool isLoading,
		DockAnimation dockAnimation,
		CrateMoveSequence crateMoveSequence,
		Action continuationCallback)
	{
		this.wsb = self.Trait<WithSpriteBody>();
		this.crateTransporter = self.Trait<CrateTransporter>();

		this.isLoading = isLoading;
		this.continuationCallback = continuationCallback;
		this.crateMoveSequence = crateMoveSequence;
		this.state = dockAnimation == DockAnimation.Docking ? DockState.Docking : DockState.Undocking;

		this.IsInterruptible = false;
	}

	protected override void OnFirstRun(Actor self)
	{
		switch (this.state)
		{
			case DockState.None:
				break;
			case DockState.Docking:
			{
				if (this.isLoading)
				{
					this.wsb.PlayCustomAnimation(self, this.DockSequence, () =>
					{
						this.wsb.PlayCustomAnimationRepeating(self, this.DockLoopSequence);
						this.state = DockState.Complete;

						// This sets crate position over the target position (i.e. on the conveyor belt) *before* the actual loading starts.
						// This is a bit hacky, but due to the architecture of DockHost/DockClient/DockClientManager it's currently necessary.
						this.UpdateCratePosition(this.crateMoveSequence);

						this.continuationCallback();
					});
				}
				else
				{
					this.UpdateCratePosition(this.crateMoveSequence);
					this.moveTick = this.crateMoveSequence.Delays[0];
				}
				break;
			}
			case DockState.Undocking:
			{
				if (this.isLoading)
				{
					this.UpdateCratePosition(this.crateMoveSequence);
					this.moveTick = this.crateMoveSequence.Delays[0];
				}
				else
				{
					this.wsb.PlayCustomAnimationBackwards(self, this.DockSequence, () =>
					{
						this.state = DockState.Complete;
						this.continuationCallback();

						this.crateTransporter.CrateOffset = WVec.Zero;
					});
				}
				break;
			}
			default:
				break;
		}
	}

	public override bool Tick(Actor self)
	{
		switch (this.state)
		{
			case DockState.Docking:
			{
				if (!this.isLoading)
				{
					if (!this.TickCrateMovement())
						return false;

					if (this.state == DockState.Complete)
					{
						this.continuationCallback();

						return true;
					}

					// Refactor/configuration?
					if (this.currentIndex == this.crateMoveSequence.Delays.Length - 1 && !this.wsb.DefaultAnimation.IsPlayingSequence(this.DockSequence))
						this.wsb.PlayCustomAnimation(self, this.DockSequence, () => this.wsb.PlayCustomAnimationRepeating(self, this.DockLoopSequence));
				}

				break;
			}
			case DockState.Undocking:
			{
				if (this.isLoading)
				{
					if (!this.TickCrateMovement())
						return false;

					if (this.state == DockState.Complete)
					{
						this.continuationCallback();
						this.crateTransporter.CrateOffset = WVec.Zero;

						return true;
					}

					// Refactor/configuration?
					if (this.currentIndex == 1 && !this.wsb.DefaultAnimation.IsPlayingSequence(this.DockSequence))
						this.wsb.PlayCustomAnimationBackwards(self, this.DockSequence);
				}

				break;
			}
			case DockState.Complete:
			{
				return true;
			}
			default:
				break;
		}

		return false;
	}

	private bool TickCrateMovement()
	{
		if (--this.moveTick >= 0)
			return false;

		++this.currentIndex;
		var index = this.currentIndex;

		if (index >= this.crateMoveSequence.Delays.Length)
		{
			this.state = DockState.Complete;

			return true;
		}

		this.moveTick = index < this.crateMoveSequence.Delays.Length ? this.crateMoveSequence.Delays[index] : 0;

		this.UpdateCratePosition(this.crateMoveSequence);

		return true;
	}

	private void UpdateCratePosition(CrateMoveSequence crateMoveSequence)
	{
		this.crateTransporter.CrateOffset = new WVec(0, 1, 0) * crateMoveSequence.Offsets[this.currentIndex];
	}
}
