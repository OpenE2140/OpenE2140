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
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Mcu;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("The MCU to building transform sequence.")]
public class TransformSequenceInfo : TraitInfo, Requires<RenderSpritesInfo>
{
	[FieldLoader.Require]
	[Desc("Image used for this decoration.")]
	public readonly string Image = string.Empty;

	[GrantedConditionReference]
	[Desc("Grant this condition while the actor is playing the sequence.")]
	public readonly string Condition = "Transforming";

	[Desc("Time it takes for the building to construct under the pyramid.")]
	public readonly int ConstructionTime = 100;

	[Desc("Sound played when actor starts transforming.", "The filename of the audio is defined per faction in notifications.yaml.")]
	public readonly string? TransformSound;

	[Desc("Offset to render the construction animation relative to the buildings top-left cell.")]
	public readonly WVec Offset = WVec.Zero;

	public override object Create(ActorInitializer init)
	{
		return new TransformSequence(init, this);
	}
}

public class TransformSequence : ITick
{
	private readonly TransformSequenceInfo info;

	private readonly RenderSprites renderSprites;

	private readonly AnimationWithOffset legacyAnimation;
	private readonly AnimationWithOffset animation;

	private int token = Actor.InvalidConditionToken;

	private int remainingTime;

	public TransformSequence(ActorInitializer init, TransformSequenceInfo info)
	{
		this.info = info;

		this.renderSprites = init.Self.TraitOrDefault<RenderSprites>();

		this.legacyAnimation = new AnimationWithOffset(new Animation(init.World, this.info.Image), () => info.Offset, () => false, _ => 0);
		this.animation = new AnimationWithOffset(new Animation(init.World, this.info.Image), () => info.Offset, () => false, _ => 0);

		var mcu = init.GetOrDefault<Mcu.McuInit>();

		if (mcu == null)
			return;

		this.StartDeploy(init.Self);
	}

	private void StartDeploy(Actor self)
	{
		this.token = self.GrantCondition(this.info.Condition);

		Game.Sound.PlayToPlayer(SoundType.World, self.Owner, this.info.TransformSound, self.CenterPosition);

		// Original construction animation has separate sprites for deploy and pyramid cover animations
		// If building is using original construction animation, we need to play cover animation
		if (this.legacyAnimation.Animation.HasSequence("cover"))
			this.DeployLegacy(self);
		else
			this.Deploy(self);
	}

	private void DeployLegacy(Actor self)
	{
		self.World.AddFrameEndTask(_ => this.renderSprites.Add(this.legacyAnimation));
		this.legacyAnimation.Animation.PlayThen("deploy", () => this.Cover(self));
	}

	private void Deploy(Actor self)
	{
		self.World.AddFrameEndTask(_ => this.renderSprites.Add(this.animation));
		this.animation.Animation.PlayThen("deploy", this.Covered);
	}

	private void Cover(Actor self)
	{
		self.World.AddFrameEndTask(_ => this.renderSprites.Add(this.animation));

		this.animation.Animation.PlayThen(
			"cover",
			() =>
			{
				self.World.AddFrameEndTask(_ => this.renderSprites.Remove(this.legacyAnimation));
				this.Covered();
			}
		);
	}

	private void Covered()
	{
		this.animation.Animation.PlayRepeating("covered");
		this.remainingTime = this.info.ConstructionTime;
	}

	void ITick.Tick(Actor self)
	{
		if (this.remainingTime == 0)
			return;

		this.remainingTime--;

		if (this.remainingTime > 0)
			return;

		self.RevokeCondition(this.token);
		this.token = Actor.InvalidConditionToken;

		this.Uncover(self);
	}

	private void Uncover(Actor self)
	{
		this.animation.Animation.PlayThen("uncover", () => self.World.AddFrameEndTask(_ => this.renderSprites.Remove(this.animation)));
	}
}
