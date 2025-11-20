#region Copyright & License Information

/*
 * Copyright (c) The OpenE2140 Developers and Contributors
 * This file is part of OpenE2140, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */

#endregion

using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits;

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
