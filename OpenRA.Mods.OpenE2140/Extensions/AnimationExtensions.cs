using OpenRA.Graphics;

namespace OpenRA.Mods.OpenE2140.Extensions;

public static class AnimationExtensions
{
	public static bool IsPlayingSequence(this Animation animation, string sequence)
	{
		return sequence.Equals(animation.CurrentSequence?.Name, StringComparison.Ordinal);
	}
}
