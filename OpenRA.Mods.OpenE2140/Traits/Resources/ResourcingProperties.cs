using OpenRA.Scripting;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

[ScriptPropertyGroup("Ability")]
public class ResourcingProperties : ScriptActorProperties, Requires<CrateTransporterRoutineInfo>
{
	private readonly CrateTransporterRoutine crateTransporterRoutine;

	public ResourcingProperties(ScriptContext context, Actor self)
		: base(context, self)
	{
		this.crateTransporterRoutine = self.Trait<CrateTransporterRoutine>();
	}

	[Desc("Start transporting resource crates.")]
	public void TransportCrates()
	{
		this.crateTransporterRoutine.StartTransporterRoutine(this.Self);
	}
}
