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

using OpenRA.Effects;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits;

public enum PipeEndDirection
{
	/// <summary>
	/// Terminal ending
	/// </summary>
	None,
	West, North, East, South
}

[Desc("Special trait for handling chained explosions of pipes.")]
public class ExplosivePipeInfo : ConditionalTraitInfo
{
	[FieldLoader.Ignore]
	private static readonly Dictionary<PipeEndDirection, CVec> PipeEndMappings = new Dictionary<PipeEndDirection, CVec>
	{
		{ PipeEndDirection.None, new CVec(0, 0) },
		{ PipeEndDirection.West, new CVec(-1, 0) },
		{ PipeEndDirection.North, new CVec(0, -1) },
		{ PipeEndDirection.East, new CVec(1, 0) },
		{ PipeEndDirection.South, new CVec(0, 1) }
	};

	[Desc("Direction of the pipe start.")]
	public readonly PipeEndDirection StartDirection = PipeEndDirection.None;

	[Desc("Direction of the pipe end.")]
	public readonly PipeEndDirection EndDirection = PipeEndDirection.None;

	[Desc("If true, this pipe is a node and it does not participate in chained explosions.")]
	public readonly bool IsNode;

	[Desc("Delay before explosion of nearest pipe, in ticks. Two values indicate a range.")]
	public readonly int[] ExplosionDelay = [10, 20];

	public override object Create(ActorInitializer init)
	{
		return new ExplosivePipe(this);
	}

	public (CVec end1, CVec end2) GetPipeEndVectors()
	{
		return (ExplosivePipeInfo.PipeEndMappings[this.StartDirection], ExplosivePipeInfo.PipeEndMappings[this.EndDirection]);
	}

	public (CVec end1, CVec end2) GetOppositePipeEndVectors()
	{
		return (ExplosivePipeInfo.PipeEndMappings[this.StartDirection] * -1, ExplosivePipeInfo.PipeEndMappings[this.EndDirection] * -1);
	}
}

public class ExplosivePipe : ConditionalTrait<ExplosivePipeInfo>, INotifyKilled
{
	public ExplosivePipe(ExplosivePipeInfo info)
		: base(info)
	{
	}

	void INotifyKilled.Killed(Actor self, AttackInfo e)
	{
		// Pipe nodes don't explode in chained reactions, but they can initiate them.
		var segments = this.GetNeighboringSegments(self).Where(p => !p.Trait.Info.IsNode);

		foreach (var pipe in segments)
		{
			var delay = self.World.SharedRandom.FromRange(this.Info.ExplosionDelay);

			self.World.AddFrameEndTask(
				world => world.Add(
					new DelayedAction(
						delay,
						() =>
						{
							if (!pipe.Actor.Disposed)
								pipe.Actor.Kill(self);
						}
					)
				)
			);
		}
	}

	private IEnumerable<TraitPair<ExplosivePipe>> GetNeighboringSegments(Actor self)
	{
		var (end1, end2) = this.Info.GetOppositePipeEndVectors();

		foreach (var actor in self.World.FindActorsInCircle(self.CenterPosition, WDist.FromCells(1)))
		{
			if (actor == self)
				continue;

			var vec = self.Location - actor.Location;

			// ignore neighboring actors that are not located at cells, where this pipe have endings
			if (vec != end1 && vec != end2)
				continue;

			if (!actor.TryGetTrait<ExplosivePipe>(out var otherPipe))
				continue;

			// check if the other pipe connects to this pipe
			if (this.IsConnected(otherPipe))
				yield return new TraitPair<ExplosivePipe>(actor, otherPipe);
		}
	}

	private bool IsConnected(ExplosivePipe otherPipe)
	{
		var (end1Opposite, end2Opposite) = this.Info.GetOppositePipeEndVectors();

		var (otherEnd1, otherEnd2) = otherPipe.Info.GetPipeEndVectors();

		return end1Opposite == otherEnd1 || end1Opposite == otherEnd2 || end2Opposite == otherEnd1 || end2Opposite == otherEnd2;
	}
}
