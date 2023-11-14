using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Extensions;

public static class HealthExtensions
{
	public static int GetHPPercentage(this IHealth health)
	{
		return health.HP * 100 / health.MaxHP;
	}
}
