using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.AI;

[TraitLocation(SystemActors.World | SystemActors.EditorWorld)]
[Desc("Handles AI debug mode state. Attach this to the world actor.")]
public class AIDebugModeInfo : TraitInfo<AIDebugMode> { }

public class AIDebugMode
{
	public bool Enable;
}
