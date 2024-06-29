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
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Helpers.Reflection;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("This actor has a conveyor belt.")]
public class ConveyorBeltInfo : DockHostInfo
{
	[GrantedConditionReference]
	[Desc("Condition to grant while animating.")]
	public readonly string Condition = "BeltActive";

	[Desc("The outside target offset of the crate.")]
	public readonly WVec OutsideOffset;

	[Desc("The inside target offset of the crate.")]
	public readonly WVec InsideOffset;

	[Desc("The Entry X coordinate for sprite cut-off.")]
	public readonly int EntryOffset;

	[Desc("The speed of the belt.")]
	public readonly int Speed = 32;

	[Desc("The ZOffset of the belt.")]
	public readonly int ZOffset;

	/// <summary>
	/// Distance between ends of the conveyor belt (i.e. distance between outside and inside position of the crate).
	/// </summary>
	public WVec DistanceBetweenEnds => this.OutsideOffset - this.InsideOffset;

	public override object Create(ActorInitializer init)
	{
		return new ConveyorBelt(init.Self, this);
	}
}

public class ConveyorBelt : DockHost, ITick, IRender, IDockHostDrag, ISubActorParent
{
	private static readonly TypeFieldHelper<Sprite> SpriteFieldHelper = ReflectionHelper.GetTypeFieldHelper<Sprite>(typeof(SpriteRenderable), "sprite");

	private int condition = Actor.InvalidConditionToken;
	private int elapsed;
	protected ResourceCrate? crate;

	private WVec DistanceBetweenEnds => this.Info.DistanceBetweenEnds;
	private int DistanceMoved => Math.Min(this.elapsed * this.Info.Speed, this.DistanceBetweenEnds.Length);

    public new ConveyorBeltInfo Info;

    public ConveyorBelt(Actor self, ConveyorBeltInfo info)
		: base(self, info)
	{
		this.Info = info;
	}

	WVec ISubActorParent.SubActorOffset
	{
		get
		{
			if (this.elapsed == 0)
				return this.Info.InsideOffset;

			var offset = this.DistanceBetweenEnds * this.DistanceMoved / this.DistanceBetweenEnds.Length;

			return this.Info.InsideOffset + offset;
		}
	}

	public bool Activate(Actor self, ResourceCrate crate)
	{
		if (this.crate != null)
			return false;

		this.condition = self.GrantCondition(this.Info.Condition);
		this.elapsed = 0;
		this.crate = crate;
		this.crate.SubActor.ParentActor = self;

		return true;
	}

	public ResourceCrate? RemoveCrate(Actor self)
	{
		var crate = this.crate;

		if (crate != null)
		{
			this.crate = null;

			crate.SubActor.ParentActor = null;
		}

		return crate;
	}

	void ITick.Tick(Actor self)
	{
		this.TickInner(self);
	}

	protected virtual void TickInner(Actor self)
	{
		// TODO: support trait pausing (necessary for power management)
		if (this.crate == null || this.IsTraitDisabled) // || this.IsTraitPaused)
			return;

		this.elapsed++;

        if (this.DistanceMoved != this.DistanceBetweenEnds.Length)
            return;

        if (this.condition != Actor.InvalidConditionToken)
			this.condition = self.RevokeCondition(this.condition);

		this.Complete(self);
	}

	protected virtual void Complete(Actor self)
	{
	}

	IEnumerable<IRenderable> IRender.Render(Actor self, WorldRenderer wr)
	{
		var result = new List<IRenderable>();

		if (this.crate == null)
			return result;

		var cutOff = self.CenterPosition.X + this.Info.EntryOffset;

		foreach (var render in this.crate.Actor.TraitsImplementing<IRender>())
			foreach (var renderable in render.Render(this.crate.Actor, wr).Select(e => e.WithZOffset(this.Info.ZOffset)).OfType<SpriteRenderable>())
			{
				if (renderable.Pos.X >= cutOff)
				{
					result.Add(renderable);

					continue;
				}

				var sprite = ConveyorBelt.SpriteFieldHelper.GetValue(renderable);

				if (sprite == null)
					continue;

				var subtract = (cutOff - renderable.Pos.X) / 16;

				if (subtract >= sprite.Bounds.Width)
					continue;

				ConveyorBelt.SpriteFieldHelper.SetValue(
					renderable,
					new Sprite(
						sprite.Sheet,
						new Rectangle(sprite.Bounds.X + subtract, sprite.Bounds.Y, sprite.Bounds.Width - subtract, sprite.Bounds.Height),
						sprite.ZRamp,
						sprite.Offset + new float3(subtract / 2f, 0, 0),
						sprite.Channel,
						sprite.BlendMode
					)
				);

				result.Add(renderable);
			}

		return result;
	}

	IEnumerable<Rectangle> IRender.ScreenBounds(Actor self, WorldRenderer wr)
	{
		var result = new List<Rectangle>();

		if (this.crate == null)
			return result;

		foreach (var render in this.crate.Actor.TraitsImplementing<IRender>())
			result.AddRange(render.ScreenBounds(this.crate.Actor, wr));

		return result;
	}
}
