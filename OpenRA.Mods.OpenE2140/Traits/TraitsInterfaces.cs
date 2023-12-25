using OpenRA.Primitives;

namespace OpenRA.Mods.OpenE2140.Traits;

public interface ISafeDragNotify
{
	void SafeDragFailed(Actor self, Actor movingActor);

	void SafeDragComplete(Actor self, Actor movingActor);
}

/// <summary>
/// Hook for modifying actor init objects in <see cref="TypeDictionary"/> before the actor is created by <see cref="Production.AnimatedExitProduction"/>.
/// </summary>
public interface IProduceActorInitModifier
{
    /// <summary>
    /// This hook is called just before the actor is created and makes it possible to modify actor init objects inside <see cref="TypeDictionary"/>.
    /// </summary>
    /// <remarks>
    /// The exact location, where the is hook called, is just before invoking
    /// <see cref="Common.Traits.Production.DoProduction(Actor, ActorInfo, Common.Traits.ExitInfo, string, TypeDictionary)"/> method.
    /// It means that this method can override any changes done by this hook.
    /// </remarks>
    void ModifyActorInit(Actor self, TypeDictionary init);
}
