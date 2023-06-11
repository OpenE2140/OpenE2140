namespace OpenRA.Mods.OpenE2140.Traits;


public interface ISafeDragNotify
{
	void SafeDragFailed(Actor self, Actor movingActor);

	void SafeDragComplete(Actor self, Actor movingActor);
}
