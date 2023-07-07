using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Hcum;

[RequireExplicitImplementation]
public interface INotifyRepair
{
	void Docking(Actor self);

	void Repairing(Actor self);

	void Undocking(Actor self);

	void Undocked(Actor self);
}
