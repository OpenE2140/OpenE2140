using OpenRA.Mods.OpenE2140.Traits.Mcu;

namespace OpenRA.Mods.OpenE2140.Traits.BotModules.Extensions;

public static class WorldExtensions
{
	public static ActorInfo? GetMcuFromActor(this OpenRA.World world, string actorName)
	{
		var actorInfo = world.Map.Rules.Actors[actorName];
		if (actorInfo.HasTraitInfo<McuInfo>())
			return actorInfo;

		return McuUtils.GetMcuActor(world, actorInfo);
	}
}
