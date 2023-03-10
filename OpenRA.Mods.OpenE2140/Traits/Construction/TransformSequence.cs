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
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Construction;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("The MCU to building transform sequence.")]
public class TransformSequenceInfo : TraitInfo, Requires<RenderSpritesInfo>
{
	[FieldLoader.RequireAttribute]
	[Desc("Image used for this decoration.")]
	public readonly string Image = "";

	[GrantedConditionReference]
	[Desc("Grant this condition while the actor is playing the sequence.")]
	public readonly string Condition = "Transforming";

	[Desc("Time it takes for the building to construct under the pyramid.")]
	public readonly int ConstructionTime = 100;

	[Desc("Sound played when actor starts transforming.", "The filename of the audio is defined per faction in notifications.yaml.")]
	public readonly string? TransformSound;

	public override object Create(ActorInitializer init)
	{
		return new TransformSequence(init, this);
	}
}

public class TransformSequence : ITick
{
	private readonly TransformSequenceInfo info;
	private readonly RenderSprites renderSprites;

	private int token = Actor.InvalidConditionToken;
	private int remainingTime = -1;
	private AnimationWithOffset? animationCover;

	public TransformSequence(ActorInitializer init, TransformSequenceInfo info)
	{
		this.info = info;

		this.renderSprites = init.Self.TraitOrDefault<RenderSprites>();
	}

	public void Run(Actor self)
	{
		this.token = self.GrantCondition(this.info.Condition);

		var animationDeploy = new AnimationWithOffset(new Animation(self.World, this.info.Image), () => WVec.Zero, () => false, _ => 0);
		self.World.AddFrameEndTask(_ => this.renderSprites.Add(animationDeploy));

		Game.Sound.PlayToPlayer(SoundType.World, self.Owner, this.info.TransformSound, self.CenterPosition);

		animationDeploy.Animation.PlayThen(
			"deploy",
			() =>
			{
				animationDeploy.Animation.PlayRepeating("deployed");

				this.animationCover = new AnimationWithOffset(new Animation(self.World, this.info.Image), () => WVec.Zero, () => false, _ => 0);
				self.World.AddFrameEndTask(_ => this.renderSprites?.Add(this.animationCover));

				this.animationCover.Animation.PlayThen(
					"cover",
					() =>
					{
						self.World.AddFrameEndTask(_ => this.renderSprites?.Remove(animationDeploy));

						this.animationCover.Animation.PlayRepeating("covered");
						this.remainingTime = this.info.ConstructionTime;
					}
				);
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
