#region Copyright & License Information

/*
 * Copyright 2007-2023 The Earth 2140 Developers (see AUTHORS)
 * This file is part of Earth 2140, which is free software. It is made
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
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.E2140.Traits.Construction;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("This actor has an elevater used for production.")]
public class ElevatorProductionInfo : ProductionInfo, Requires<RenderSpritesInfo>
{
	[FieldLoader.RequireAttribute]
	[Desc("Image used for the elevator.")]
	public readonly string Image = "";

	[Desc("Elevator Position.")]
	public readonly WVec Position;

	[Desc("Elevator height.")]
	public readonly int Height = 1024;

	[Desc("How long it takes the elevator to reach the top.")]
	public readonly int Duration = 25;

	public override object Create(ActorInitializer init)
	{
		return new ElevatorProduction(init, this);
	}
}

public class ElevatorProduction : Production, INotifyCreated, ITick, IRender
{
	private record ProductionInfo(ActorInfo Producee, ExitInfo ExitInfo, string ProductionType, TypeDictionary Inits);

	private enum State
	{
		Closed, Opening, ElevatorUp, Ejecting, ElevatorDown, Closing
	}

	private readonly ElevatorProductionInfo info;

	private RenderSprites? renderSprites;

	private State state = State.Closed;

	private AnimationWithOffset? animationBase;
	private AnimationWithOffset? animationElevator;
	private AnimationWithOffset? animationOverlay;
	private int stateStartTick;

	private ProductionInfo? productionInfo;

	public ElevatorProduction(ActorInitializer init, ElevatorProductionInfo info)
		: base(init, info)
	{
		this.info = info;
	}

	public override void DoProduction(Actor self, ActorInfo producee, ExitInfo exitinfo, string productionType, TypeDictionary inits)
	{
		if (this.state != State.Closed)
			return;

		this.productionInfo = new ProductionInfo(producee, exitinfo, productionType, inits);

		this.state = State.Opening;
		this.stateStartTick = self.World.WorldTick;

		this.animationBase?.Animation.PlayThen(
			"opening",
			() =>
			{
				this.animationBase?.Animation.PlayRepeating("open");

				this.state = State.ElevatorUp;
				this.stateStartTick = self.World.WorldTick;
			}
		);
	}

	void INotifyCreated.Created(Actor self)
	{
		this.renderSprites = self.TraitOrDefault<RenderSprites>();

		this.animationBase = new AnimationWithOffset(
			new Animation(self.World, this.info.Image),
			() => this.info.Position,
			() => this.IsTraitDisabled,
			_ => -this.info.Height - this.info.Position.Y - 1
		);

		this.animationBase.Animation.PlayRepeating("closed");
		self.World.AddFrameEndTask(_ => this.renderSprites?.Add(this.animationBase));

		this.animationElevator = new AnimationWithOffset(
			new Animation(self.World, this.info.Image),
			() => this.info.Position + new WVec(0, 0, this.GetElevatorHeight(self)),
			() => this.IsTraitDisabled || this.state is not State.ElevatorUp and not State.Ejecting and not State.ElevatorDown,
			_ => -this.info.Position.Y - 1
		);

		this.animationElevator.Animation.PlayRepeating("elevator");
		self.World.AddFrameEndTask(_ => this.renderSprites?.Add(this.animationElevator));

		this.animationOverlay = new AnimationWithOffset(
			new Animation(self.World, this.info.Image),
			() => this.info.Position,
			() => this.IsTraitDisabled,
			_ => -this.info.Position.Y - 1
		);

		this.animationOverlay.Animation.PlayRepeating("overlay");
		self.World.AddFrameEndTask(_ => this.renderSprites?.Add(this.animationOverlay));
	}

	private int GetElevatorHeight(Actor self)
	{
		return this.state switch
		{
			State.ElevatorUp => -(this.info.Height - (self.World.WorldTick - this.stateStartTick) * this.info.Height / this.info.Duration),
			State.ElevatorDown => -(self.World.WorldTick - this.stateStartTick) * this.info.Height / this.info.Duration,
			_ => 0
		};
	}

	void ITick.Tick(Actor self)
	{
		if (this.IsTraitDisabled)
			return;

		switch (this.state)
		{
			case State.ElevatorUp:
				if (self.World.WorldTick - this.stateStartTick < this.info.Duration)
					return;

				this.state = State.Ejecting;
				this.stateStartTick = self.World.WorldTick;

				if (this.productionInfo != null)
				{
					base.DoProduction(
						self,
						this.productionInfo.Producee,
						this.productionInfo.ExitInfo,
						this.productionInfo.ProductionType,
						this.productionInfo.Inits
					);

					this.productionInfo = null;
				}

				break;

			case State.Ejecting:
				// TODO wait for produced actor to leave platform.
				if (self.World.WorldTick - this.stateStartTick < 25)
					return;

				this.state = State.ElevatorDown;
				this.stateStartTick = self.World.WorldTick;

				break;

			case State.ElevatorDown:
				if (self.World.WorldTick - this.stateStartTick < this.info.Duration)
					return;

				this.state = State.Closing;
				this.stateStartTick = self.World.WorldTick;

				this.animationBase?.Animation.PlayBackwardsThen(
					"opening",
					() =>
					{
						this.state = State.Closed;
						this.stateStartTick = self.World.WorldTick;

						this.animationBase.Animation.PlayRepeating("closed");
					}
				);

				break;

			case State.Closed:
			case State.Opening:
			case State.Closing:
				break;

			default:
				throw new ArgumentOutOfRangeException(nameof(this.state), "Unknown state.");
		}
	}

	IEnumerable<IRenderable> IRender.Render(Actor self, WorldRenderer worldRenderer)
	{
		if (this.state != State.ElevatorUp)
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

		return actorPreviews.SelectMany(
			actorPreview => actorPreview.Render(worldRenderer, self.CenterPosition + this.info.Position + new WVec(0, 0, this.GetElevatorHeight(self)))
		);
	}

	IEnumerable<Rectangle> IRender.ScreenBounds(Actor self, WorldRenderer worldRenderer)
	{
		return this.renderSprites?.ScreenBounds(self, worldRenderer) ?? Array.Empty<Rectangle>();
	}
}
