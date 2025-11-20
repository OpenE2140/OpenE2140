namespace OpenRA.Mods.OpenE2140.Traits;

public interface INotifySelfDestruction
{
	void SelfDestructionStarted(Actor self);

	void SelfDestructionAborted(Actor self);
}
