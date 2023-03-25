using JetBrains.Annotations;
using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits;

[UsedImplicitly]
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

	public void Enable(Actor actor, string sound)
	{
		if (!this.playing.TryGetValue(sound, out var entry))
			this.playing.Add(sound, entry = new Entry(Game.Sound.PlayLooped(SoundType.World, sound, actor.CenterPosition), new List<Actor> { actor }));

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
				if (entry.Sound == null || entry.Sound.Complete == true)
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
