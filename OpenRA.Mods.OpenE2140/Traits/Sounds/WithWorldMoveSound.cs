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
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Sounds;

[UsedImplicitly]
[TraitLocation(SystemActors.World)]
[Desc("Prevents WithMoveSound to play multiple times.")]
public class WithWorldMoveSoundInfo : TraitInfo
{
	public override object Create(ActorInitializer init)
	{
		return new WithWorldMoveSound();
	}
}

public class WithWorldMoveSound : ITick, IWorldLoaded
{
	private record Entry(ISound Sound, List<Actor> Actors);

	private readonly Dictionary<string, Entry> playing = new Dictionary<string, Entry>();
	private WorldRenderer? worldRenderer;

	void IWorldLoaded.WorldLoaded(World world, WorldRenderer worldRenderer)
	{
		this.worldRenderer = worldRenderer;
	}

	public void Enable(Actor actor, string soundName)
	{
		if (!this.playing.TryGetValue(soundName, out var entry))
		{
			var sound = Game.Sound.PlayLooped(SoundType.World, soundName, actor.CenterPosition);

			if (sound == null)
				return;

			this.playing.Add(soundName, entry = new Entry(sound, new List<Actor> { actor }));
		}

		if (!entry.Actors.Contains(actor))
			entry.Actors.Add(actor);

		Game.Sound.SetLooped(entry.Sound, true);
	}

	public void Disable(Actor actor, string sound)
	{
		if (!this.playing.TryGetValue(sound, out var entry))
			return;

		entry.Actors.Remove(actor);

		if (!entry.Actors.Any())
			Game.Sound.SetLooped(entry.Sound, false);
	}

	void ITick.Tick(Actor self)
	{
		foreach (var sound in this.playing.Keys.ToArray())
		{
			var entry = this.playing[sound];

			if (!entry.Actors.Any())
			{
				if (entry.Sound.Complete)
					this.playing.Remove(sound);
			}
			else if (this.worldRenderer != null)
			{
				Game.Sound.SetPosition(
					entry.Sound,
					entry.Actors.MinBy(actor => (actor.CenterPosition - this.worldRenderer.Viewport.CenterPosition).Length).CenterPosition
				);
			}
		}
	}
}
