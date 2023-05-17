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

using JetBrains.Annotations;
using OpenRA.Activities;
using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.OpenE2140.Graphics;
using OpenRA.Mods.OpenE2140.Traits.Rendering;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Production;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("This actor has an elevater used for production.")]
public class ElevatorProductionInfo : ProductionInfo, IRenderActorPreviewSpritesInfo, Requires<RenderSpritesInfo>
{
	[FieldLoader.Require]
	[Desc("Image used for the elevator.")]
	public readonly string Image = "";

	[Desc("Elevator Position.")]
	public readonly WVec Position;

	[Desc("Ensure the elevator is beneath everything.")]
	public readonly int ZOffset = -1024;

	[Desc("Elevator height.")]
	public readonly int Height = 1024;

	[Desc("Cut-Off row in pixels.")]
	public readonly int CutOff;

	[Desc("How long it takes the elevator to reach the top.")]
	public readonly int Duration = 25;

	public override object Create(ActorInitializer init)
	{
		return new ElevatorProduction(init, this);
	}

	IEnumerable<IActorPreview> IRenderActorPreviewSpritesInfo.RenderPreviewSprites(
		ActorPreviewInitializer init,
		string image,
		int facings,
		PaletteReference palette
	)
	{
		var animation = new Animation(init.World, this.Image);
		animation.PlayRepeating("closed");

		yield return new SpriteActorPreview(animation, () => this.Position, () => -this.Height - this.Position.Y - 1, palette);
	}
}

public class ElevatorProduction : Common.Traits.Production, ITick, IRender, INotifyProduction
{
	/// <summary>
	/// When current or all exits are blocked, nudge surrounding units every X ticks.
	/// </summary>
	private const int NudgeAfterTicks = 100;

	/// <summary>
	/// When no exit is currently available, wait X ticks until attempting to eject produced unit again.
	/// </summary>
	private const int WaitingForEjectionDelay = 15;

	/// <summary>
	/// When current exit is currently blocked, wait X ticks until giving up and running retry logic.
	/// </summary>
	private const int EjectionWaitLimit = 25;

	private record ProductionInfo(ActorInfo Producee, ExitInfo ExitInfo, string ProductionType, TypeDictionary Inits, Actor? Actor)
	{
		public Activity? ExitMoveActivity;

		public void TryQueuingPathToRallyPoint(RallyPoint rp)
		{
			// Queue path to rally point only if current activity has been ordered by ElevatorProduction (and not player).
			// If player has moved produced actor, it's safe to assume they wanted to override the default behavior (of moving to rally point).
			if (this.Actor == null || this.Actor.CurrentActivity != this.ExitMoveActivity)
				return;

			foreach (var cell in rp.Path)
			{
				this.Actor.QueueActivity(
					new AttackMoveActivity(
						this.Actor,
						() => this.Actor.Trait<IMove>().MoveTo(cell, 1, evaluateNearestMovableCell: true, targetLineColor: Color.OrangeRed)
					)
				);
			}
		}
	}

	public enum AnimationState
	{
		Closed, Opening, ElevatorUp, Ejecting, WaitingForEjection, ElevatorDown, Closing
	}

	private readonly ElevatorProductionInfo info;
	private readonly RenderSprites? renderSprites;
	private readonly AnimationWithOffset animation;
	private readonly ElevatorProductionQueue productionQueue;
	private AnimationState state = AnimationState.Closed;
	private int stateAge;
	private int? lastNudge;
	private ProductionInfo? productionInfo;
	private ProductionInfo? lastProducedUnit;
	private RallyPoint? rallyPoint;

	public Actor Actor { get; }

	public AnimationState State
	{
		get => this.state;
		private set
		{
			this.state = value;
			this.stateAge = 0;
		}
	}

	public CPos ElevatorCell => this.Actor.World.Map.CellContaining(this.Actor.CenterPosition + this.info.Position);
	public WPos ElevatorCellCenter => this.Actor.World.Map.CenterOfCell(this.ElevatorCell);

	public ElevatorProduction(ActorInitializer init, ElevatorProductionInfo info)
		: base(init, info)
	{
		this.info = info;
		this.Actor = init.Self;

		this.renderSprites = init.Self.TraitOrDefault<RenderSprites>();

		this.animation = new AnimationWithOffset(
			new Animation(init.Self.World, this.info.Image),
			() => this.info.Position,
			() => this.IsTraitDisabled,
			_ => this.info.ZOffset - this.info.Height
		);

		this.animation.Animation.PlayRepeating("closed");
		init.Self.World.AddFrameEndTask(_ => this.renderSprites?.Add(this.animation));

		var animationElevator = new CutOffAnimationWithOffset(
			new Animation(init.Self.World, this.info.Image),
			() => this.info.Position + new WVec(0, 0, this.GetElevatorHeight()),
			() => this.IsTraitDisabled || this.State is AnimationState.Closed or AnimationState.Opening or AnimationState.Closing,
			_ => this.info.ZOffset,
			() => this.info.Position.Y + this.info.CutOff * 16
		);

		animationElevator.Animation.PlayRepeating("elevator");
		init.Self.World.AddFrameEndTask(_ => this.renderSprites?.Add(animationElevator));

		var animationOverlay = new AnimationWithOffset(
			new Animation(init.Self.World, this.info.Image),
			() => this.info.Position,
			() => this.IsTraitDisabled,
			_ => this.info.ZOffset
		);

		animationOverlay.Animation.PlayRepeating("overlay");
		init.Self.World.AddFrameEndTask(_ => this.renderSprites?.Add(animationOverlay));

		this.productionQueue = init.Self.Trait<ElevatorProductionQueue>();
	}

	protected override void Created(Actor self)
	{
		this.rallyPoint = self.TraitOrDefault<RallyPoint>();
		base.Created(self);
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

		this.productionInfo = new ProductionInfo(producee, exitInfo, productionType, inits, null);

		this.State = AnimationState.Opening;

		this.animation.Animation.PlayThen(
			"opening",
			() =>
			{
				this.animation.Animation.PlayRepeating("open");

				this.State = AnimationState.ElevatorUp;
			}
		);

		// Preemptively nudge any blocking actors out of the way now
		var exitCell = self.Location + this.productionInfo.ExitInfo.ExitCell;
		this.NudgeBlockingActors(self, exitCell);
	}

	private bool NudgeBlockingActors(Actor self, CPos targetCell, params Actor[] exceptActors)
	{
		if (this.lastNudge != null && this.lastNudge + ElevatorProduction.NudgeAfterTicks > self.World.WorldTick)
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
				actor.QueueActivity(false, mobile?.MoveTo(cell.Value, 0));
		}

		return true;
	}

	private int GetElevatorHeight()
	{
		return this.State switch
		{
			AnimationState.ElevatorUp => -(this.info.Height - this.stateAge * this.info.Height / this.info.Duration),
			AnimationState.ElevatorDown => -(this.stateAge * this.info.Height / this.info.Duration),
			_ => 0
		};
	}

	void ITick.Tick(Actor self)
	{
		if (this.IsTraitDisabled)
			return;

		switch (this.state)
		{
			case AnimationState.ElevatorUp:
			{
				if (this.IsTraitPaused)
					return;

				if (this.stateAge++ < this.info.Duration)
					return;

				this.State = AnimationState.Ejecting;

				if (this.productionInfo == null)
					break;

				var exit = self.Location + this.productionInfo.ExitInfo.ExitCell;

				var initialFacing = this.productionInfo.ExitInfo.Facing
					?? ElevatorProduction.GetInitialFacing(this.productionInfo.Producee, this.ElevatorCellCenter, self.World.Map.CenterOfCell(exit));

				var inits = this.productionInfo.Inits;
				inits.Add(new LocationInit(self.World.Map.CellContaining(this.ElevatorCellCenter)));
				inits.Add(new CenterPositionInit(this.ElevatorCellCenter));
				inits.Add(new FacingInit(initialFacing));

				Log.Write(
					"debug",
					$"ElevatorProduction: creating actor: {this.productionInfo.Producee.Name}, location: {self.World.Map.CellContaining(this.ElevatorCellCenter)}, exit: {exit}"
				);

				base.DoProduction(self, this.productionInfo.Producee, null, this.productionInfo.ProductionType, inits);

				break;
			}

			case AnimationState.Ejecting:
			{
				var actor = this.productionInfo?.Actor;

				if (this.productionInfo == null || actor == null || actor.IsInWorld == false)
				{
					// actor was likely destroyed, close elevator
					this.State = AnimationState.ElevatorDown;
					this.productionInfo = null;

					break;
				}

				var exitCell = self.Location + this.productionInfo.ExitInfo.ExitCell;

				Log.Write(
					"debug",
					$"ElevatorProduction: ejecting actor: {this.productionInfo.Actor}, location: {self.World.Map.CellContaining(actor.CenterPosition)}, exit: {exitCell}"
				);

				if (this.NudgeBlockingActors(self, exitCell, actor))
					this.State = AnimationState.WaitingForEjection;
				else if (actor.CurrentActivity is null || actor.CurrentActivity is not Move)
				{
					// Abort all activites except Move to prevent player from delaying the exit.
					actor.CancelActivity();

					// Store move activity that is queued by this ElevatorProduction, it's going to be needed later.
					this.productionInfo = this.productionInfo with { ExitMoveActivity = actor.Trait<IMove>().MoveTo(exitCell) };
					actor.QueueActivity(this.productionInfo.ExitMoveActivity);
				}

				// Check if actor is no longer on the elevator cell. Only checking whether actor has moved to
				// exit cell might be insufficient and in rare cases can elevator get stuck in this state.
				if (self.World.Map.CellContaining(actor.CenterPosition) != this.ElevatorCell)
				{
					this.State = AnimationState.ElevatorDown;

					if (this.rallyPoint?.Path.Count > 0)
						this.productionInfo.TryQueuingPathToRallyPoint(this.rallyPoint);

					this.lastProducedUnit = this.productionInfo;
					this.productionInfo = null;
				}

				// If current exit cell is still blocked after some time, try again in next state (WaitingForEjection),
				// which can pick another exit.
				if (this.stateAge++ >= ElevatorProduction.EjectionWaitLimit)
					this.State = AnimationState.WaitingForEjection;

				break;
			}

			case AnimationState.WaitingForEjection:
			{
				var actor = this.productionInfo?.Actor;

				if (this.productionInfo == null || actor == null || actor.IsInWorld == false)
				{
					// actor was likely destroyed, close elevator
					this.State = AnimationState.ElevatorDown;
					this.productionInfo = null;

					break;
				}

				// player manually moved produced actor
				var actorCell = self.World.Map.CellContaining(actor.CenterPosition);

				if (actorCell != this.ElevatorCell)
				{
					Log.Write(
						"debug",
						$"ElevatorProduction: WaitingForEjection, actor '{this.productionInfo.Actor}' has been manually moved to exit: {actorCell}"
					);

					this.State = AnimationState.ElevatorDown;

					this.lastProducedUnit = this.productionInfo;
					this.productionInfo = null;

					break;
				}

				if (this.stateAge++ < ElevatorProduction.WaitingForEjectionDelay)
					break;

				var exitCell = self.Location + this.productionInfo.ExitInfo.ExitCell;

				Log.Write("debug", $"ElevatorProduction: WaitingForEjection, actor: {this.productionInfo.Actor}, current exit: {exitCell}");

				var blockingActors = self.World.ActorMap.GetActorsAt(exitCell).Exclude(actor).ToArray();

				if (!blockingActors.Any())
				{
					Log.Write("debug", $"ElevatorProduction: WaitingForEjection, actor '{this.productionInfo.Actor}' can be ejected, exit: {exitCell}");

					// Store move activity that is queued by this ElevatorProduction, it's going to be needed later.
					this.productionInfo = this.productionInfo with { ExitMoveActivity = actor.Trait<IMove>().MoveTo(exitCell) };
					actor.QueueActivity(this.productionInfo.ExitMoveActivity);
					this.State = AnimationState.Ejecting;

					break;
				}

				// current exit cell is still blocked, try picking another
				var nextFreeExit = this.SelectExit(self, actor.Info, this.productionInfo.ProductionType);

				if (nextFreeExit != null)
				{
					Log.Write(
						"debug",
						$"ElevatorProduction: WaitingForEjection, found new exit for actor '{this.productionInfo!.Actor}', ejecting, new exit: {exitCell}"
					);

					this.productionInfo = this.productionInfo with { ExitInfo = nextFreeExit.Info };
					actor.CancelActivity();
					this.State = AnimationState.Ejecting;

					break;
				}

				// still no exit available, try picking any other passable ...
				var randomExit = this.SelectAnyPassableExit(self, this.productionInfo.Producee, this.productionInfo.ProductionType)!;
				exitCell = self.Location + randomExit.Info.ExitCell;
				this.productionInfo = this.productionInfo with { ExitInfo = randomExit.Info };

				// ... and nudge any actors at that cell
				this.NudgeBlockingActors(self, exitCell, actor);

				this.stateAge = 0;

				break;
			}

			case AnimationState.ElevatorDown:
				if (this.IsTraitPaused)
					return;

				if (this.stateAge++ < this.info.Duration)
					return;

				this.State = AnimationState.Closing;

				this.animation.Animation.PlayBackwardsThen(
					"opening",
					() =>
					{
						this.State = AnimationState.Closed;

						this.animation.Animation.PlayRepeating("closed");

						this.productionQueue.UnitCompleted(this.lastProducedUnit!.Actor!);
					}
				);

				break;

			case AnimationState.Closed:
			case AnimationState.Opening:
			case AnimationState.Closing:
				break;

			default:
				throw new ArgumentOutOfRangeException(nameof(this.state), "Unknown state.");
		}
	}

	private static WAngle GetInitialFacing(ActorInfo producee, WPos spawn, WPos target)
	{
		WAngle initialFacing;
		var delta = target - spawn;

		if (delta.HorizontalLengthSquared == 0)
		{
			var fi = producee.TraitInfoOrDefault<IFacingInfo>();
			initialFacing = fi != null ? fi.GetInitialFacing() : WAngle.Zero;
		}
		else
			initialFacing = delta.Yaw;

		return initialFacing;
	}

	void INotifyProduction.UnitProduced(Actor self, Actor other, CPos exit)
	{
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
			e => producee?.TraitInfo<MobileInfo>()
					?.CanEnterCell(self.World, self, self.Location + e.Info.ExitCell, ignoreActor: self, check: BlockedByActor.Immovable)
				== true
		);
	}

	IEnumerable<IRenderable> IRender.Render(Actor self, WorldRenderer worldRenderer)
	{
		if (this.State != AnimationState.ElevatorUp)
			return Array.Empty<IRenderable>();

		if (this.productionInfo == null)
			return Array.Empty<IRenderable>();

		var renderActorPreview = this.productionInfo.Producee.TraitInfos<IRenderActorPreviewInfo>().FirstOrDefault();

		if (renderActorPreview == null)
			return Array.Empty<IRenderable>();

		var previewInit = new TypeDictionary { new FacingInit(this.productionInfo.ExitInfo.Facing ?? new WAngle()) };

		foreach (var init in this.productionInfo.Inits)
			previewInit.Add(init);

		var actorPreviews = renderActorPreview.RenderPreview(
			new ActorPreviewInitializer(new ActorReference(this.productionInfo.Producee.Name, previewInit), worldRenderer)
		);

		var renderables = actorPreviews
			.SelectMany(actorPreview => actorPreview.Render(worldRenderer, this.ElevatorCellCenter + new WVec(0, 0, this.GetElevatorHeight())))
			.Select(
				renderable => renderable is SpriteRenderable spriteRenderable
					? spriteRenderable.WithZOffset(spriteRenderable.ZOffset + this.info.ZOffset)
					: renderable
			)
			.ToArray();

		RenderElevatorSprites.PostProcess(renderables, this.GetElevatorHeight() + this.info.CutOff * 16);

		return renderables;
	}

	IEnumerable<Rectangle> IRender.ScreenBounds(Actor self, WorldRenderer worldRenderer)
	{
		return this.renderSprites?.ScreenBounds(self, worldRenderer) ?? Array.Empty<Rectangle>();
	}
}
