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

using JetBrains.Annotations;

namespace OpenRA.Mods.OpenE2140.Traits;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class EncyclopediaInfo : Common.Traits.EncyclopediaInfo
{
	[Desc("The file with animation to play.",
		"Currently supported are all video formats that the OpenRA engine supports plus sprite sheets (in any supported format).")]
	public readonly string? Animation;

	[Desc("The Long title.")]
	public readonly string? Title;

	[Desc("Just meta data which we display properly...")]
	public readonly string? Armor;

	public readonly string? Armament;
	public readonly string? Resistance;
}
