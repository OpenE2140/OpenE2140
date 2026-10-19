namespace OpenRA.Mods.OpenE2140.Traits.Mcu;

public class McuBuildingMap
{
	public List<McuBuildingMapping> Mappings { get; init; } = [];

	public IEnumerable<ActorInfo> McuActors => this.Mappings.Select(m => m.McuActor);
	public IEnumerable<ActorInfo> BuildingActors => this.Mappings.Select(m => m.BuildingActor);

	public bool HasMcuMapping(ActorInfo mcuActor)
	{
		return this.Mappings.Any(m => m.McuActor == mcuActor);
	}

	public bool HasBuildingMapping(ActorInfo buildingActor)
	{
		return this.Mappings.Any(m => m.BuildingActor == buildingActor);
	}

	public McuBuildingMapping? GetByMcu(ActorInfo mcuActor)
	{
		return this.Mappings.FirstOrDefault(m => m.McuActor == mcuActor);
	}

	public McuBuildingMapping? GetByBuilding(ActorInfo buildingActor)
	{
		return this.Mappings.FirstOrDefault(m => m.BuildingActor == buildingActor);
	}

	public ActorInfo? GetBuildingByMcu(ActorInfo mcuActor)
	{
		return this.GetByMcu(mcuActor)?.BuildingActor;
	}

	public ActorInfo? GetMcuByBuilding(ActorInfo buildingActor)
	{
		return this.GetByBuilding(buildingActor)?.McuActor;
	}

	public static McuBuildingMap Create(Ruleset rules, IReadOnlyCollection<string> buildingActorNames)
	{
		var list = new List<McuBuildingMapping>(buildingActorNames.Count);

		foreach (var buildingActorName in buildingActorNames)
		{
			if (!rules.Actors.TryGetValue(buildingActorName, out var buildingActor))
				continue;

			var mcuActor = McuUtils.GetMcuActor(rules, buildingActor);
			if (mcuActor == null)
				continue;

			list.Add(new McuBuildingMapping(mcuActor, buildingActor));
		}

		return new McuBuildingMap { Mappings = list };
	}
}

public class McuBuildingMapping
{
	public ActorInfo McuActor { get; }
	public ActorInfo BuildingActor { get; }
	public ICustomBuildingInfo? BuildingInfo { get; }

	public CVec[] FootprintOffsets { get; } = [];

	public CVec TransformOffset { get; }

	public McuBuildingMapping(ActorInfo mcuActor, ActorInfo buildingActor)
	{
		this.McuActor = mcuActor;
		this.BuildingActor = buildingActor;
		this.TransformOffset = mcuActor.TraitInfo<TransformsInfo>().Offset;
		this.BuildingInfo = CustomBuildingInfoWrapper.WrapIfNecessary(this.BuildingActor);

		if (this.BuildingInfo != null)
			this.FootprintOffsets = this.BuildingInfo.Footprint().Keys.Select(v => v + this.TransformOffset).ToArray();
	}
}
