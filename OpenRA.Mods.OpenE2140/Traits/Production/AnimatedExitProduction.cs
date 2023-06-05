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

using System.Reflection;
using JetBrains.Annotations;
using OpenRA.Activities;
using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Production;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("This actor has an animated exit used for production.")]
public class AnimatedExitProductionInfo : ProductionInfo, IRenderActorPreviewSpritesInfo, Requires<RenderSpritesInfo>
{
	[Desc("Image used for the Exit.")]
	public readonly string? Image;

	[Desc("The sequence to use for the closed state.")]
	public string SequenceClosed = "closed";

	[Desc("The sequence to use for the opening animation.")]
	public string SequenceOpening = "opening";

	[Desc("The sequence to use for the open state.")]
	public string SequenceOpen = "open";

	[Desc("The sequence to use for the closing animation.")]
	public string SequenceClosing = "closing";

	[Desc("The sequence to use for the overlay.")]
	public string SequenceOverlay = "overlay";

	[Desc("Animated exit Position.")]
	public readonly WVec Position;

	[Desc("Animated exit Z-Offset.")]
	public readonly int ZOffset;

	[Desc("When current or all exits are blocked, nudge surrounding units every X ticks.")]
	public readonly int NudgeAfterTicks = 100;

	[Desc("When no exit is currently available, wait X ticks until attempting to eject produced unit again.")]
	public readonly int WaitingForEjectionDelay = 15;

	[Desc("When current exit is currently blocked, wait X ticks until giving up and running retry logic.")]
	public readonly int EjectionWaitLimit = 25;

	public override object Create(ActorInitializer init)
	{
		return new AnimatedExitProduction(init, this);
	}

	IEnumerable<IActorPreview> IRenderActorPreviewSpritesInfo.RenderPreviewSprites(
		ActorPreviewInitializer init,
		string image,
		int facings,
		PaletteReference palette
	)
	{
		var animation = new Animation(init.World, this.Image ?? image);

		if (!animation.HasSequence(this.SequenceClosed))
			yield break;

		animation.PlayRepeating(this.SequenceClosed);

		yield return new SpriteActorPreview(animation, () => this.Position, this.GetZOffset, palette);
	}

	public virtual int GetZOffset()
	{
		return 0;
	}
}

public class AnimatedExitProduction : Common.Traits.Production, ITick, INotifyProduction
{
	protected record ProductionInfo(
		ActorInfo Producee,
		ExitInfo ExitInfo,
		string ProductionType,
		TypeDictionary Inits,
		Actor? Actor,
		Activity? ExitMoveActivity
	);

	public enum AnimationState
	{
		Closed, Opening, Ejecting, WaitingForEjection, Closing, Custom
	}

	private readonly AnimatedExitProductionInfo info;

	protected readonly RenderSprites RenderSprites;
	private RallyPoint? rallyPoint;

	private readonly AnimationWithOffset animation;
	private bool animationVisible;

	private readonly AnimatedExitProductionQueue productionQueue;
	private int? lastNudge;
	protected ProductionInfo? productionInfo;
	private ProductionInfo? lastProducedUnit;

	private AnimationState state = AnimationState.Closed;
	protected int stateAge;

	public AnimationState State
	{
		get => this.state;
		protected set
		{
			this.state = value;
			this.stateAge = 0;
		}
	}

	public AnimatedExitProduction(ActorInitializer init, AnimatedExitProductionInfo info)
		: base(init, info)
	{
		this.info = info;

		this.RenderSprites = init.Self.Trait<RenderSprites>();

		this.animation = new AnimationWithOffset(
			new Animation(init.Self.World, this.info.Image ?? this.RenderSprites.GetImage(init.Self)),
			() => this.info.Position,
			() => this.IsTraitDisabled || !this.animationVisible,
			_ => this.info.ZOffset + this.info.GetZOffset()
		);

		this.PlayAnimation(this.info.SequenceClosed);
		init.Self.World.AddFrameEndTask(_ => this.RenderSprites?.Add(this.animation));

		var animationOverlay = new AnimationWithOffset(
			new Animation(init.Self.World, this.info.Image ?? this.RenderSprites.GetImage(init.Self)),
			() => this.info.Position,
			() => this.IsTraitDisabled,
			_ => this.info.ZOffset
		);

		if (animationOverlay.Animation.HasSequence(this.info.SequenceOverlay))
		{
			animationOverlay.Animation.PlayRepeating(this.info.SequenceOverlay);
			init.Self.World.AddFrameEndTask(_ => this.RenderSprites?.Add(animationOverlay));
		}

		this.productionQueue = init.Self.Trait<AnimatedExitProductionQueue>();
	}

	protected override void Created(Actor self)
	{
		this.rallyPoint = self.TraitOrDefault<RallyPoint>();
		base.Created(self);
	}

	// Allows to play optional animations.
	private void PlayAnimation(string sequence)
	{
		this.animationVisible = this.animation.Animation.HasSequence(sequence);

		if (this.animationVisible)
			this.animation.Animation.PlayRepeating(sequence);
	}

	public override bool Produce(Actor self, ActorInfo producee, string productionType, TypeDictionary inits, int refundableValue)
	{
		if (this.IsTraitDisabled || this.IsTraitPaused || Reservable.IsReserved(self) || this.State != AnimationState.Closed)
			return false;

		// Pick a spawn/exit point pair
		var exit = this.SelectExit(self, producee, productionType);

		this.DoProduction(self, producee, exit?.Info, productionType, inits);

		return true;
	}

	public override void DoProduction(Actor self, ActorInfo producee, ExitInfo? exitInfo, string productionType, TypeDictionary inits)
	{
		if (this.State != AnimationState.Closed)
			return;

		// If no exit was specified, pick one (even if it isn't free)
		exitInfo ??= this.SelectAnyPassableExit(self, producee, productionType).Info;

		this.productionInfo = new ProductionInfo(producee, exitInfo, productionType, inits, null, null);

		// Preemptively nudge any blocking actors out of the way now
		this.NudgeBlockingActors(self, self.Location + this.productionInfo.ExitInfo.ExitCell);

		this.Open(self);
	}

	private bool NudgeBlockingActors(Actor self, CPos targetCell, params Actor[] exceptActors)
	{
		if (this.lastNudge != null && this.lastNudge + this.info.NudgeAfterTicks > self.World.WorldTick)
			return false;

		var blockingActors = self.World.ActorMap.GetActorsAt(targetCell).Except(exceptActors).ToArray();

		if (!blockingActors.Any())
			return false;

		this.lastNudge = self.World.WorldTick;

		foreach (var actor in blockingActors)
		{
			var mobile = actor.TraitOrDefault<Mobile>();
			var cell = mobile?.GetAdjacentCell(targetCell);

			if (cell != null)
				actor.QueueActivity(false, mobile?.MoveTo(cell.Value));
		}

		return true;
	}

	void ITick.Tick(Actor self)
	{
		if (this.IsTraitDisabled)
			return;

		switch (this.state)
		{
			case AnimationState.Ejecting:
			{
				var actor = this.productionInfo?.Actor;

				if (this.productionInfo == null || actor == null || actor.IsInWorld == false)
				{
					// actor was likely destroyed, close exit
					this.productionInfo = null;
					this.Close(self);

					break;
				}

				var exitCell = self.Location + this.productionInfo.ExitInfo.ExitCell;

				if (this.productionInfo.Producee.HasTraitInfo<AircraftInfo>())
				{
					// Aircraft do not need custom logic to handle their exit from this Production trait.
					// So just store the creation activity (likely going to be AssociateWithAirfieldActivity) for handling rally point.
					this.productionInfo = this.productionInfo with { ExitMoveActivity = actor.CurrentActivity };
				}
				else if (this.NudgeBlockingActors(self, exitCell, actor))
					this.State = AnimationState.WaitingForEjection;
				else if (actor.CurrentActivity is null || (actor.CurrentActivity is not Move && actor.CurrentActivity is not Mobile.ReturnToCellActivity))
				{
					// Abort all activites except Move to prevent player from delaying the exit.
					actor.CancelActivity();

					// Store move activity that is queued by this AnimatedExitProduction, it's going to be needed later.
					var activity = actor.Trait<IMove>().MoveTo(exitCell);
					var mobile = actor.TraitOrDefault<Mobile>();

					if (mobile != null)
					{
						var pos = actor.CenterPosition;
						mobile.SetPosition(actor, exitCell);
						mobile.SetCenterPosition(actor, pos);
						activity = mobile.ReturnToCell(actor);
					}

					this.productionInfo = this.productionInfo with { ExitMoveActivity = activity };
					actor.QueueActivity(this.productionInfo.ExitMoveActivity);
				}

				// Check if actor is no longer on the exit cell. Only checking whether actor has moved to
				// exit cell might be insufficient and in rare cases can exit get stuck in this state.
				// If produced actor is aircraft, it's sufficient, that it's airborne, to consider ejection process complete.
				if (actor.TraitOrDefault<Aircraft>()?.AtLandAltitude == false ||
					self.World.Map.CellContaining(actor.CenterPosition) != this.GetExitCell(self))
				{
					this.QueuePathToRallyPoint(this.productionInfo);

					this.lastProducedUnit = this.productionInfo;
					this.productionInfo = null;

					this.Close(self);
				}

				// If current exit cell is still blocked after some time, try again in next state (WaitingForEjection),
				// which can pick another exit.
				if (this.stateAge++ >= this.info.EjectionWaitLimit)
					this.State = AnimationState.WaitingForEjection;

				break;
			}

			case AnimationState.WaitingForEjection:
			{
				var actor = this.productionInfo?.Actor;

				if (this.productionInfo == null || actor == null || actor.IsInWorld == false)
				{
					// actor was likely destroyed, close exit
					this.productionInfo = null;

					this.Close(self);

					break;
				}

				// If aircraft is airborne or player has manually moved produced actor, end ejection process
				if (actor.TraitOrDefault<Aircraft>()?.AtLandAltitude == false ||
					self.World.Map.CellContaining(actor.CenterPosition) != this.GetExitCell(self))
				{
					this.QueuePathToRallyPoint(this.productionInfo);

					this.lastProducedUnit = this.productionInfo;
					this.productionInfo = null;

					this.Close(self);

					break;
				}

				if (this.stateAge++ < this.info.WaitingForEjectionDelay)
					break;

				var exitCell = self.Location + this.productionInfo.ExitInfo.ExitCell;

				var blockingActors = self.World.ActorMap.GetActorsAt(exitCell).Exclude(actor).ToArray();

				if (!blockingActors.Any())
				{
					// Store move activity that is queued by this AnimatedExitProduction, it's going to be needed later.
					this.productionInfo = this.productionInfo with { ExitMoveActivity = actor.Trait<IMove>().MoveTo(exitCell) };
					actor.QueueActivity(this.productionInfo.ExitMoveActivity);
					this.State = AnimationState.Ejecting;

					break;
				}

				// current exit cell is still blocked, try picking another
				var nextFreeExit = this.SelectExit(self, actor.Info, this.productionInfo.ProductionType);

				if (nextFreeExit != null)
				{
					this.productionInfo = this.productionInfo with { ExitInfo = nextFreeExit.Info };
					this.State = AnimationState.Ejecting;

					break;
				}

				// still no exit available, try picking any other passable ...
				var randomExit = this.SelectAnyPassableExit(self, this.productionInfo.Producee, this.productionInfo.ProductionType);
				exitCell = self.Location + randomExit.Info.ExitCell;
				this.productionInfo = this.productionInfo with { ExitInfo = randomExit.Info };

				// ... and nudge any actors at that cell
				this.NudgeBlockingActors(self, exitCell, actor);

				this.stateAge = 0;

				break;
			}

			case AnimationState.Closed:
			case AnimationState.Opening:
			case AnimationState.Closing:
				break;

			case AnimationState.Custom:
			{
				this.TickCustom(self);

				break;
			}

			default:
				throw new ArgumentOutOfRangeException(nameof(this.state), "Unknown state.");
		}
	}

	private void QueuePathToRallyPoint(ProductionInfo productionInfo)
	{
		var actor = productionInfo.Actor;
		if (actor == null || this.rallyPoint == null)
			return;

		// Queue path to rally point only if current activity has been ordered by this Production trait (and not player).
		// If player has moved produced actor, it's safe to assume they wanted to override the default behavior (of moving to rally point).
		if (actor.CurrentActivity != productionInfo.ExitMoveActivity)
			return;
		
		foreach (var cell in this.rallyPoint.Path)
		{
			actor.QueueActivity(
				new AttackMoveActivity(
					actor,
					() => actor.Trait<IMove>().MoveTo(cell, 1, evaluateNearestMovableCell: true, targetLineColor: Color.OrangeRed)
				)
			);
		}
	}

	// Allows subclasses to tick custom states.
	protected virtual void TickCustom(Actor self)
	{
		throw new NotImplementedException();
	}

	// Called when we should open.
	protected virtual void Open(Actor self)
	{
		this.State = AnimationState.Opening;

		// TODO First frame is skipped...?!
		this.animation.Animation.PlayThen(
			this.info.SequenceOpening,
			() =>
			{
				this.PlayAnimation(this.info.SequenceOpen);
				self.World.AddFrameEndTask(_ => this.Opened(self));
			}
		);
	}

	// Called when we opened
	protected virtual void Opened(Actor self)
	{
		this.Eject(self);
	}

	// Called when we should eject the produces actor.
	protected virtual void Eject(Actor self)
	{
		this.State = AnimationState.Ejecting;

		if (this.productionInfo == null)
			return;

		var exit = self.Location + this.productionInfo.ExitInfo.ExitCell;
		var exitCenter = this.GetExitCellCenter(self);
		var spawnLocation = this.GetSpawnLocation(self, exit);

		var initialFacing = this.productionInfo.ExitInfo.Facing
			?? AnimatedExitProduction.GetInitialFacing(this.productionInfo.Producee, spawnLocation, self.World.Map.CenterOfCell(exit));

		var inits = this.productionInfo.Inits;
		inits.Add(new LocationInit(self.World.Map.CellContaining(exitCenter)));
		inits.Add(new CenterPositionInit(spawnLocation));
		inits.Add(new FacingInit(initialFacing));

		base.DoProduction(self, this.productionInfo.Producee, null, this.productionInfo.ProductionType, inits);
	}

	protected virtual WPos GetSpawnLocation(Actor self, CPos exitCell)
	{
		return this.GetExitCellCenter(self) + this.productionInfo?.ExitInfo.SpawnOffset ?? WPos.Zero;
	}

	// Called when we should close.
	protected virtual void Close(Actor self)
	{
		this.State = AnimationState.Closing;

		this.animation.Animation.PlayThen(
			this.info.SequenceClosing,
			() =>
			{
				this.PlayAnimation(this.info.SequenceClosed);
				self.World.AddFrameEndTask(_ => this.Closed(self));
			}
		);
	}

	// Called when we closed
	protected virtual void Closed(Actor self)
	{
		this.State = AnimationState.Closed;
		this.productionQueue.UnitCompleted(this.lastProducedUnit!.Actor!);
	}

	private static WAngle GetInitialFacing(ActorInfo producee, WPos spawn, WPos target)
	{
		WAngle initialFacing;
		var delta = target - spawn;

		if (delta.HorizontalLengthSquared == 0)
			initialFacing = producee.TraitInfoOrDefault<IFacingInfo>()?.GetInitialFacing() ?? WAngle.Zero;
		else
			initialFacing = delta.Yaw;

		return initialFacing;
	}

	void INotifyProduction.UnitProduced(Actor self, Actor other, CPos exit)
	{
		// Mobile ICreationActivity queued an uncancelable ReturnToCellActivity activity -.-
		// This looks horrible when not spawned at a cell center! (see infantry walking into the house before exiting)
		if (other.CurrentActivity is Mobile.ReturnToCellActivity)
			other.GetType().GetField("currentActivity", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(other, other.CurrentActivity.NextActivity);

		if (this.productionInfo != null)
			this.productionInfo = this.productionInfo with { Actor = other };
	}

	private Exit SelectAnyPassableExit(Actor self, ActorInfo producee, string productionType)
	{
		// Passable exit is cell, which is passable for produced actor, while ignoring any movable actors.
		return this.SelectExit(
			self,
			producee,
			productionType,
			e => producee.TraitInfo<MobileInfo>()
					?.CanEnterCell(self.World, self, self.Location + e.Info.ExitCell, ignoreActor: self, check: BlockedByActor.Immovable)
				== true
		);
	}

	private CPos GetExitCell(Actor self)
	{
		return self.World.Map.CellContaining(self.CenterPosition + this.info.Position);
	}

	protected WPos GetExitCellCenter(Actor self)
	{
		return self.World.Map.CenterOfCell(this.GetExitCell(self));
	}
}
