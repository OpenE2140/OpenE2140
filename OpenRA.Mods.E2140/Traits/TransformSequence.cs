#region Copyright & License Information

/*
 * Copyright 2007-2022 The Earth 2140 Developers (see AUTHORS)
 * This file is part of Earth 2140, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */

#endregion

using JetBrains.Annotations;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Traits;

namespace OpenRA.Mods.E2140.Traits;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class TransformSequenceInfo : TraitInfo
{
	[Desc("Image used for this decoration.")]
	public readonly string? Image;

	[Desc("Grant this condition while the actor is playing the sequence.")]
	public readonly string Condition = "Transforming";

	[Desc("Time it takes for the building to construct under the pyramid.")]
	public readonly int ConstructionTime = 100;

	public override object Create(ActorInitializer init)
	{
		return new TransformSequence(this);
	}
}

public class TransformSequence : INotifyCreated, ITick
{
	private readonly TransformSequenceInfo info;
	private RenderSprites? renderSprites;

	private int token = Actor.InvalidConditionToken;
	private int remainingTime = -1;
	private AnimationWithOffset? animationCover;
	private AnimationWithOffset? animationDeployMask;

	public TransformSequence(TransformSequenceInfo info)
	{
		this.info = info;
	}

	void INotifyCreated.Created(Actor self)
	{
		this.renderSprites = self.TraitOrDefault<RenderSprites>();
	}

	public void Run(Actor self)
	{
		this.token = self.GrantCondition(this.info.Condition);

		var animationDeploy = new AnimationWithOffset(new(self.World, this.info.Image), () => WVec.Zero, () => false, _ => 0);
		self.World.AddFrameEndTask(_ => this.renderSprites?.Add(animationDeploy));

		animationDeploy.Animation.PlayThen(
			"deploy",
			() =>
			{
				animationDeploy.Animation.PlayRepeating("deployed");

				this.animationCover = new(new(self.World, this.info.Image), () => WVec.Zero, () => false, _ => 0);
				self.World.AddFrameEndTask(_ => this.renderSprites?.Add(this.animationCover));

				this.animationCover.Animation.PlayThen(
					"cover",
					() =>
					{
						self.World.AddFrameEndTask(_ =>
						{
							this.renderSprites?.Remove(animationDeploy);
							this.renderSprites?.Remove(this.animationDeployMask);
						});
						this.animationCover.Animation.PlayRepeating("covered");
						this.remainingTime = this.info.ConstructionTime;
					}
				);
			}
		);

		this.animationDeployMask = new AnimationWithOffset(new(self.World, this.info.Image), () => WVec.Zero, () => false, _ => 0);
		self.World.AddFrameEndTask(_ => this.renderSprites?.Add(this.animationDeployMask));

		this.animationDeployMask.Animation.PlayThen(
			"deploy_mask",
			() =>
			{
				this.animationDeployMask.Animation.PlayRepeating("deployed_mask");
			}
		);
	}

	void ITick.Tick(Actor self)
	{
		if (this.remainingTime < 0)
			return;

		this.remainingTime--;

		if (this.remainingTime >= 0)
			return;

		self.RevokeCondition(this.token);
		this.token = Actor.InvalidConditionToken;

		this.animationCover?.Animation.PlayBackwardsThen("cover", () => self.World.AddFrameEndTask(_ => this.renderSprites?.Remove(this.animationCover)));
	}
}
