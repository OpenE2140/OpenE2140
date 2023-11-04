using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;

namespace OpenRA.Mods.OpenE2140.Traits.Radar;

public class CraterRadarSignatureInfo : ConditionalTraitInfo
{
	public override object Create(ActorInitializer init)
	{
		return new CraterRadarSignature(this);
	}
}

public class CraterRadarSignature : ConditionalTrait<CraterRadarSignatureInfo>, IRadarSignature
{
	private static readonly CVec[] CraterPattern =
	{
		new CVec(0, -2),
		new CVec(-1, -1), new CVec(0, -1), new CVec(1, -1),
		new CVec(-2, 0), new CVec(-1, 0), new CVec(0, 0), new CVec(1, 0), new CVec(2, 0),
		new CVec(-1, 1), new CVec(0, 1), new CVec(1, 1),
		new CVec(0, 2),
	};

	public CraterRadarSignature(CraterRadarSignatureInfo info)
		: base(info)
	{
	}

	void IRadarSignature.PopulateRadarSignatureCells(Actor self, List<(CPos Cell, Color Color)> destinationBuffer)
	{
		if (this.IsTraitDisabled)
			return;

		destinationBuffer.AddRange(CraterPattern.Select(v => (self.Location + v, Color.White)));
	}
}
