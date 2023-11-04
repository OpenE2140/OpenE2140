using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.BuildingCrew;

[RequireExplicitImplementation]
public interface INotifyCrewMemberEntered
{
	void OnCrewMemberEntered(Actor self, Actor crewMember);
}

[RequireExplicitImplementation]
public interface INotifyCrewMemberExited
{
	void OnCrewMemberExited(Actor self, Actor crewMember);
}

[RequireExplicitImplementation]
public interface INotifyEnteredBuildingCrew
{
	void OnEnteredBuildingCrew(Actor self, Actor buildingCrew);
}

[RequireExplicitImplementation]
public interface INotifyExitedBuildingCrew
{
	void OnExitedBuildingCrew(Actor self, Actor buildingCrew);
}

[RequireExplicitImplementation]
public interface INotifyEnterCrewMember
{
	void Entering(Actor self);
}

[RequireExplicitImplementation]
public interface INotifyBuildingCrewExit
{
	void Exiting(Actor self);
}

[RequireExplicitImplementation]
public interface INotifyBuildingConquered
{
	void OnConquering(Actor self, Actor conqueror, Player oldOwner, Player newOwner);
}
