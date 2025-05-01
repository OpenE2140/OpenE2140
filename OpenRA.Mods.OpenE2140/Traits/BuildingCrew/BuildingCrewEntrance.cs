using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.BuildingCrew;

[Desc($"Defines entrance for a building. Requires {nameof(BuildingCrew)} defined on the actor to work.")]
public class BuildingCrewEntranceInfo : ConditionalTraitInfo
{
	[Desc("Offset towards which the entering actor is moving to enter the building. Relative to the center of the building actor.",
		"If null, center of the nearest footprint cell is chosen.")]
	public readonly WVec? EntranceOffset;

	[Desc("Cell offset from where the entering actor enters the building. Relative to the topleft cell of the building actor.")]
	public readonly CVec EntryCell = CVec.Zero;

	[Desc("Entries with larger priorities will be used before lower priorities.")]
	public readonly int Priority = 1;

	public override object Create(ActorInitializer init)
	{
		return new BuildingCrewEntrance(init.Self, this);
	}
}

public class BuildingCrewEntrance : ConditionalTrait<BuildingCrewEntranceInfo>
{
	private readonly Actor self;
	private readonly IEnumerable<CPos> footprintCells;

	private WVec? baseEntranceOffset;

	public CPos EntryCell { get; init; }

	public WPos Target { get; init; }

	public BuildingCrewEntrance(Actor self, BuildingCrewEntranceInfo info)
		: base(info)
	{
		this.self = self;
		this.EntryCell = this.self.Location + info.EntryCell;
		this.footprintCells = this.self.Info.TraitInfo<BuildingInfo>().FootprintTiles(this.self.Location, FootprintCellType.Occupied);
	}

	protected override void Created(Actor self)
	{
		base.Created(self);

		this.baseEntranceOffset = this.Info.EntranceOffset ?? this.self.Info.TraitInfo<BuildingCrewInfo>().DefaultEntranceOffset;
	}

	public WPos GetEntranceOffset()
	{
		if (this.baseEntranceOffset != null)
			return this.self.CenterPosition + this.baseEntranceOffset.Value;

		var entranceCell = this.footprintCells.MinBy(cell => (cell - this.EntryCell).LengthSquared);
		return this.self.World.Map.CenterOfCell(entranceCell);
	}

	public WPos GetExitOffset()
	{
		if (this.baseEntranceOffset != null)
			return this.self.CenterPosition + this.baseEntranceOffset.Value;

		return this.self.World.Map.CenterOfCell(this.footprintCells.MinBy(cell => (this.EntryCell - cell).LengthSquared));
	}
}
