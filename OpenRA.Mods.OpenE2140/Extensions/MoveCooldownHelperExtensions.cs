using System.Diagnostics.CodeAnalysis;
using OpenRA.Mods.Common.Activities;

namespace OpenRA.Mods.OpenE2140.Extensions;

public static class MoveCooldownHelperExtensions
{
	public static bool TryTick(this MoveCooldownHelper moveCooldownHelper, bool targetIsHiddenActor, [NotNullWhen(true)] out bool? result)
	{
		result = moveCooldownHelper.Tick(targetIsHiddenActor);
		return result != null;
	}
}
