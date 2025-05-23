namespace OpenRA.Mods.OpenE2140.Traits.Buildings;

public interface INotifySelfDestruction
{
	void SelfDestructionStarted(Actor self);

	void SelfDestructionAborted(Actor self);
}
