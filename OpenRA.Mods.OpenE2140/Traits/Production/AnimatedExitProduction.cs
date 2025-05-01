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
using OpenRA.Mods.OpenE2140.Activites.Move;
using OpenRA.Mods.OpenE2140.Extensions;
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

	[Desc("Whether the open sequence should be looped.")]
	public bool SequenceOpenLoop = true;

	[Desc("The sequence to use for the closing animation.")]
	public string SequenceClosing = "closing";

	[Desc("The sequence to use for the overlay.")]
	public string SequenceOverlay = "overlay";

	[Desc("The sound(s) to play at the start of the opening animation.")]
	public string[] SoundsOpening = [];

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

	[Desc("Minimum delay between production of two actors, in ticks.")]
	public readonly int MinimumTicksBetweenProduction = 3;

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
	private readonly List<IProduceActorInitModifier> actorInitModifiers;

	protected readonly RenderSprites RenderSprites;
	private AnimatedExitProductionQueue[] productionQueues = [];
	private RallyPoint? rallyPoint;

	private readonly AnimationWithOffset animation;
	private bool animationVisible;

	private int? lastNudge;
	protected ProductionInfo? productionInfo;
	private ProductionInfo? lastProducedUnit;
	private int? lastProducedUnitTick;

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

	/// <summary>
	/// Returns <c>true</c>, if this <see cref="AnimatedExitProduction"/> can build new unit at this precise moment.
	/// Takes into account the minimum delay specified by <see cref="AnimatedExitProductionInfo.MinimumTicksBetweenProduction"/>.
	/// </summary>
	public bool CanBuildUnitNow =>
		this.State == AnimationState.Closed
		&& (this.lastProducedUnitTick == null || Game.LocalTick - this.lastProducedUnitTick >= this.info.MinimumTicksBetweenProduction);

	public AnimatedExitProduction(ActorInitializer init, AnimatedExitProductionInfo info)
		: base(init, info)
	{
		this.info = info;
		this.actorInitModifiers = init.Self.TraitsImplementing<IProduceActorInitModifier>().ToList();

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
	}

	protected override void Created(Actor self)
	{
		this.rallyPoint = self.TraitOrDefault<RallyPoint>();
		this.productionQueues = self.TraitsImplementing<AnimatedExitProductionQueue>().ToArray();

		base.Created(self);
	}

	// Allows to play optional animations.
	private void PlayAnimation(string sequence, bool repeat = true)
	{
		this.animationVisible = !string.IsNullOrEmpty(sequence) && this.animation.Animation.HasSequence(sequence);

		if (this.animationVisible)
		{
			if (repeat)
				this.animation.Animation.PlayRepeating(sequence);
			else
				this.animation.Animation.PlayThen(sequence, () => this.animationVisible = false);
		}
	}

	private void PlayAnimationThen(string sequence, Action action)
	{
		this.animationVisible = !string.IsNullOrEmpty(sequence) && this.animation.Animation.HasSequence(sequence);

		if (this.animationVisible)
			this.animation.Animation.PlayThen(sequence, action);
	}

	public override bool Produce(Actor self, ActorInfo producee, string productionType, TypeDictionary inits, int refundableValue)
	{
		if (this.IsTraitDisabled || this.IsTraitPaused || Reservable.IsReserved(self) || !this.CanBuildUnitNow)
			return false;

		// Pick a spawn/exit point pair
		var exit = this.SelectExit(self, producee, productionType);

		this.DoProduction(self, producee, exit?.Info, productionType, inits);

		return true;
	}

	public override void DoProduction(Actor self, ActorInfo producee, ExitInfo? exitInfo, string productionType, TypeDictionary inits)
	{
		if (!this.CanBuildUnitNow)
			return;

		// If no exit was specified, pick one (even if it isn't free)
		exitInfo ??= this.SelectAnyPassableExit(self, producee, productionType)?.Info;
		if (exitInfo == null)
		{
			// there's literaly no passable exit around, don't produce the actor now
			return;
		}

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

		if (blockingActors.Length == 0)
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

				// Check if actor is no longer on the exit cell. Only checking whether actor has moved to
				// exit cell might be insufficient and in rare cases can exit get stuck in this state.
				// If produced actor is aircraft, consider ejection process complete.
				if (self.World.Map.CellContaining(actor.CenterPosition) != this.GetSpawnCell(self))
				{
					this.QueuePathToRallyPoint(this.productionInfo);
					this.NotifyProducedUnit(self, actor);

					this.lastProducedUnit = this.productionInfo;
					this.productionInfo = null;

					this.Close(self);
					break;
				}

				// Only Mobile actors need custom logic to handle their exit from this Production trait.
				if (this.productionInfo.Producee.HasTraitInfo<MobileInfo>())
				{
					if (this.NudgeBlockingActors(self, exitCell, actor))
						this.State = AnimationState.WaitingForEjection;
					else if (actor.CurrentActivity is null || (actor.CurrentActivity is not Move && actor.CurrentActivity is not ProductionExitMove))
					{
						// Abort all activites except ProductionExitMove to prevent player from delaying the exit.
						actor.CancelActivity();

						// Using standard MoveTo always moves actor from one cell center to another (or subcell in case of actor located within subcell)
						// Using activity similar to LocalMove (i.e. Drag) *is* necessary, because that moves actor from precise WPos to another WPos
						// ProductionExitMove is safer variant of Drag that does checks not to move onto blocked cell.

						var activity = new ProductionExitMove(actor, self, self.World.Map.CenterOfCell(exitCell));

						// Store move activity that is queued by this AnimatedExitProduction, it's going to be needed later.
						this.productionInfo = this.productionInfo with { ExitMoveActivity = activity };
						actor.QueueActivity(this.productionInfo.ExitMoveActivity);
					}
				}
				else if (actor.TryGetTrait<Aircraft>(out var aircraft))
				{
					// When Aircraft is produced, the exit should be closed immediately
					if (this.rallyPoint == null)
						this.QueuePathToRallyPoint(this.productionInfo);
					else
						actor.QueueActivity(aircraft.MoveTo(exitCell));

					this.NotifyProducedUnit(self, actor);

					this.lastProducedUnit = this.productionInfo;
					this.productionInfo = null;

					this.Close(self);
					break;
				}

				// If produced actor cannot exit exit cell after some time, try again in next state (WaitingForEjection),
				// which can pick another exit.
				if (this.stateAge++ >= this.info.EjectionWaitLimit && this.GetSpawnPosition(self, exitCell) == actor.CenterPosition)
					this.State = AnimationState.WaitingForEjection;

				// Just failsafe
				if (this.stateAge >= this.info.EjectionWaitLimit * 2)
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

				// If player has manually moved produced actor, end ejection process
				if (self.World.Map.CellContaining(actor.CenterPosition) != this.GetSpawnCell(self))
				{
					this.QueuePathToRallyPoint(this.productionInfo);
					this.NotifyProducedUnit(self, actor);

					this.lastProducedUnit = this.productionInfo;
					this.productionInfo = null;

					this.Close(self);

					break;
				}

				if (this.stateAge++ < this.info.WaitingForEjectionDelay)
					break;

				var exitCell = self.Location + this.productionInfo.ExitInfo.ExitCell;

				var blockingActors = self.World.ActorMap.GetActorsAt(exitCell).Exclude(actor).ToArray();

				if (blockingActors.Length == 0)
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
				if (randomExit != null)
				{
					exitCell = self.Location + randomExit.Info.ExitCell;
					this.productionInfo = this.productionInfo with { ExitInfo = randomExit.Info };
				}

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

	private void NotifyProducedUnit(Actor self, Actor actor)
	{
		foreach (var notifier in actor.TryGetTraitsImplementing<INotifyActorProduced>())
			notifier.OnProduced(actor, self);
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
				new AttackMoveActivity(actor, () => actor.Trait<IMove>().MoveTo(cell, 1, evaluateNearestMovableCell: true, targetLineColor: Color.OrangeRed))
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

		void AfterSequenceOpened()
		{
			if (this.info.SequenceOpenLoop)
				this.PlayAnimation(this.info.SequenceOpen);
			else
				this.PlayAnimation(this.info.SequenceOpen, false);

			self.World.AddFrameEndTask(_ => this.Opened(self));
		}

		// TODO First frame is skipped...?!
		if (!string.IsNullOrEmpty(this.info.SequenceOpening) && this.animation.Animation.HasSequence(this.info.SequenceOpening))
			this.PlayAnimationThen(this.info.SequenceOpening, AfterSequenceOpened);
		else
			AfterSequenceOpened();

		foreach (var file in this.info.SoundsOpening)
			Game.Sound.PlayToPlayer(SoundType.World, self.Owner, file, self.CenterPosition);
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
		var spawnPosition = this.GetSpawnPosition(self, exit);
		var spawnCell = self.World.Map.CellContaining(spawnPosition);

		var initialFacing = this.productionInfo.ExitInfo.Facing
			?? AnimatedExitProduction.GetInitialFacing(this.productionInfo.Producee, spawnPosition, self.World.Map.CenterOfCell(exit));

		var inits = this.productionInfo.Inits;
		inits.Add(new LocationInit(spawnCell));
		inits.Add(new FacingInit(initialFacing));

		// HACK: CenterPositionInit is necessary, because otherwise oldPos private field in Mobile trait remains 0,0,0 after actor is created
		// this causes Mobile.UpdateMovement to determine that Actor has moved (from 0,0,0 to spawn offset)
		inits.Add(new CenterPositionInit(spawnPosition));

		//base.DoProduction(self, this.productionInfo.Producee, null, this.productionInfo.ProductionType, inits);
		this.DoProductionBase(self, this.productionInfo.Producee, null, this.productionInfo.ProductionType, inits);
	}

	/// <summary>
	/// Calls <see cref="Common.Traits.Production.DoProduction(Actor, ActorInfo, ExitInfo, string, TypeDictionary)"/>.
	/// </summary>
	protected void DoProductionBase(Actor self, ActorInfo producee, ExitInfo? exitInfo, string productionType, TypeDictionary inits)
	{
		this.actorInitModifiers.ForEach(m => m.ModifyActorInit(self, inits));

		base.DoProduction(self, producee, exitInfo, productionType, inits);
	}

	// Called when we should close.
	protected virtual void Close(Actor self)
	{
		this.State = AnimationState.Closing;

		void AfterSequenceClosed()
		{
			this.PlayAnimation(this.info.SequenceClosed);
			self.World.AddFrameEndTask(_ => this.Closed(self));
		}

		if (!string.IsNullOrEmpty(this.info.SequenceClosing) && this.animation.Animation.HasSequence(this.info.SequenceClosing))
			this.PlayAnimationThen(this.info.SequenceClosing, AfterSequenceClosed);
		else
			AfterSequenceClosed();
	}

	// Called when we closed
	protected virtual void Closed(Actor self)
	{
		this.lastNudge = 0;
		this.State = AnimationState.Closed;

		if (this.lastProducedUnit != null)
		{
			// Multiple queues could have produced the unit, find out which one should be notified.
			var productionQueue = this.productionQueues.Single(q => q.Info.Type == this.lastProducedUnit.ProductionType);
			productionQueue.UnitCompleted(this.lastProducedUnit.Actor!);

			// Store tick, when this AnimatedExitProduction has closed for the last produced unit.
			// Will be used in check CanBuildUnitNow above.
			this.lastProducedUnitTick = Game.LocalTick;
		}
	}

	protected static WAngle GetInitialFacing(ActorInfo producee, WPos spawn, WPos target)
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
		// HACK: LeaveProductionActivity activity is queued because CenterPositionInit was used when creating Actor (see Eject() above).
		// We need to cancel bypass it via reflection, since it is not cancellable.

		// TODO: PR to make LeaveProductionActivity on actor creation optional when CenterPositionInit is used
		if (other.CurrentActivity is Mobile.LeaveProductionActivity)
			other.GetType().GetField("currentActivity", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(other, other.CurrentActivity.NextActivity);

		this.OnUnitProduced(self, other, exit);

		if (this.productionInfo != null)
			this.productionInfo = this.productionInfo with { Actor = other };
	}

	protected virtual void OnUnitProduced(Actor self, Actor other, CPos exit)
	{
		var spawnPosition = this.GetSpawnPosition(self, exit);

		if (other.TryGetTrait<Mobile>(out var mobile))
			mobile.SetCenterPosition(other, spawnPosition);
		else if (other.TryGetTrait<Aircraft>(out var aircraft))
			aircraft.SetCenterPosition(other, spawnPosition);
	}

	private Exit? SelectAnyPassableExit(Actor self, ActorInfo producee, string productionType)
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

	protected virtual WPos GetSpawnPosition(Actor self, CPos exitCell)
	{
		return self.CenterPosition + this.info.Position;
	}

	protected virtual CPos GetSpawnCell(Actor self)
	{
		return self.World.Map.CellContaining(self.CenterPosition + this.info.Position);
	}
}
