#region Copyright & License Information

/*
 * Copyright 2007-2023 The OpenE2140 Developers (see AUTHORS)
 * This file is part of OpenE2140, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */

#endregion

using JetBrains.Annotations;
using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.OpenE2140.Graphics;
using OpenRA.Mods.OpenE2140.Traits.Rendering;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Construction;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("This actor has an elevater used for production.")]
public class ElevatorProductionInfo : ProductionInfo, IRenderActorPreviewSpritesInfo, Requires<RenderSpritesInfo>
{
	[FieldLoader.RequireAttribute]
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

public class ElevatorProduction : Production, ITick, IRender, INotifyProduction
{
	private record ProductionInfo(ActorInfo Producee, ExitInfo ExitInfo, string ProductionType, TypeDictionary Inits, Actor? Actor);

	public enum AnimationState
	{
		Closed, Opening, ElevatorUp, Ejecting, ElevatorDown, Closing
	}

	private readonly ElevatorProductionInfo info;

	private readonly RenderSprites? renderSprites;
	private readonly AnimationWithOffset animation;
	private readonly ElevatorProductionQueue productionQueue;
	private AnimationState state = AnimationState.Closed;
	private int stateAge;
	private ProductionInfo? productionInfo;
	private ProductionInfo? lastProducedUnit;

	public AnimationState State
	{
		get => this.state;
		private set
		{
			this.state = value;
			this.stateAge = 0;
		}
	}

	public ElevatorProduction(ActorInitializer init, ElevatorProductionInfo info)
		: base(init, info)
	{
		this.info = info;

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

	public override void DoProduction(Actor self, ActorInfo producee, ExitInfo exitinfo, string productionType, TypeDictionary inits)
	{
		if (this.State != AnimationState.Closed)
			return;

		this.productionInfo = new ProductionInfo(producee, exitinfo, productionType, inits, null);

		this.State = AnimationState.Opening;

		this.animation.Animation.PlayThen(
			"opening",
			() =>
			{
				this.animation.Animation.PlayRepeating("open");

				this.State = AnimationState.ElevatorUp;
			}
		);
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
				if (this.stateAge++ < this.info.Duration)
					return;

				this.State = AnimationState.Ejecting;

				if (this.productionInfo != null)
				{
					base.DoProduction(
						self,
						this.productionInfo.Producee,
						this.productionInfo.ExitInfo,
						this.productionInfo.ProductionType,
						this.productionInfo.Inits
					);
				}

				break;

			case AnimationState.Ejecting:
				var actor = this.productionInfo?.Actor;

				if (actor != null && (!actor.IsInWorld || actor.CurrentActivity is not Mobile.ReturnToCellActivity))
				{
					this.State = AnimationState.ElevatorDown;

					this.lastProducedUnit = this.productionInfo;
					this.productionInfo = null;
				}

				break;

			case AnimationState.ElevatorDown:
				if (this.stateAge++ < this.info.Duration)
					return;

				this.State = AnimationState.Closing;

				this.animation.Animation.PlayBackwardsThen(
					"opening",
					() =>
					{
						this.State = AnimationState.Closed;

						this.animation.Animation.PlayRepeating("closed");

						this.productionQueue.UnitCompleted(this, this.lastProducedUnit!.Actor!);
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

	void INotifyProduction.UnitProduced(Actor self, Actor other, CPos exit)
	{
		if (this.productionInfo != null)
			this.productionInfo = this.productionInfo with { Actor = other };
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
			.SelectMany(actorPreview => actorPreview.Render(worldRenderer, self.CenterPosition + this.info.Position + new WVec(0, 0, this.GetElevatorHeight())))
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
