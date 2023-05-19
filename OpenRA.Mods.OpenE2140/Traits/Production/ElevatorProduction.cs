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
using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Graphics;
using OpenRA.Mods.OpenE2140.Traits.Rendering;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Production;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("This actor has an elevator used for production.")]
public class ElevatorProductionInfo : AnimatedExitProductionInfo
{
	[Desc("Elevator height.")]
	public readonly int Height = 1024;

	[Desc("Cut-Off row in pixels.")]
	public readonly int CutOff;

	[Desc("How long it takes the elevator to reach the top.")]
	public readonly int Duration = 25;

	[Desc("The sequence to use for the elevator platform.")]
	public string SequenceElevator = "elevator";

	public override object Create(ActorInitializer init)
	{
		return new ElevatorProduction(init, this);
	}

	public override int GetZOffset()
	{
		return -this.Height - this.Position.Y - 1;
	}
}

public class ElevatorProduction : AnimatedExitProduction, IRender
{
	private enum CustomAnimationState
	{
		None, ElevatorUp, ElevatorDown
	}

	private readonly ElevatorProductionInfo info;
	private CustomAnimationState customState = CustomAnimationState.None;

	public ElevatorProduction(ActorInitializer init, ElevatorProductionInfo info)
		: base(init, info)
	{
		this.info = info;

		var animationElevator = new CutOffAnimationWithOffset(
			new Animation(init.Self.World, this.info.Image ?? this.RenderSprites.GetImage(init.Self)),
			() => this.info.Position + new WVec(0, 0, this.GetElevatorHeight()),
			() => this.IsTraitDisabled || this.State is AnimationState.Closed or AnimationState.Opening or AnimationState.Closing,
			_ => this.info.ZOffset,
			() => this.info.Position.Y + this.info.CutOff * 16
		);

		animationElevator.Animation.PlayRepeating(this.info.SequenceElevator);
		init.Self.World.AddFrameEndTask(_ => this.RenderSprites.Add(animationElevator));
	}

	private int GetElevatorHeight()
	{
		return this.customState switch
		{
			CustomAnimationState.ElevatorUp => -(this.info.Height - this.stateAge * this.info.Height / this.info.Duration),
			CustomAnimationState.ElevatorDown => -(this.stateAge * this.info.Height / this.info.Duration),
			_ => 0
		};
	}

	protected override void TickCustom(Actor self)
	{
		switch (this.customState)
		{
			case CustomAnimationState.ElevatorUp:
			{
				if (!this.IsTraitPaused && this.stateAge++ >= this.info.Duration)
				{
					this.customState = CustomAnimationState.None;
					this.Eject(self);
				}

				break;
			}

			case CustomAnimationState.ElevatorDown:
			{
				if (!this.IsTraitPaused && this.stateAge++ >= this.info.Duration)
				{
					this.customState = CustomAnimationState.None;
					base.Close(self);
				}

				break;
			}

			case CustomAnimationState.None:
			default:
				throw new ArgumentOutOfRangeException();
		}
	}

	protected override void Opened(Actor self)
	{
		this.State = AnimationState.Custom;
		this.customState = CustomAnimationState.ElevatorUp;
	}

	protected override void Close(Actor self)
	{
		this.State = AnimationState.Custom;
		this.customState = CustomAnimationState.ElevatorDown;
	}

	protected override WPos GetSpawnLocation(Actor self, CPos exitCell)
	{
		return this.GetExitCellCenter(self);
	}

	IEnumerable<IRenderable> IRender.Render(Actor self, WorldRenderer worldRenderer)
	{
		if (this.customState != CustomAnimationState.ElevatorUp)
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
			.SelectMany(actorPreview => actorPreview.Render(worldRenderer, this.GetExitCellCenter(self) + new WVec(0, 0, this.GetElevatorHeight())))
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
		return this.RenderSprites.ScreenBounds(self, worldRenderer) ?? Array.Empty<Rectangle>();
	}
}
