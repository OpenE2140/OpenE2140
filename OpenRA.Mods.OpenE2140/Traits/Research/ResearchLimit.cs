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

using System.Collections.ObjectModel;
using JetBrains.Annotations;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[Desc("Add this to the world, to limit the research level.")]
public class ResearchLimitInfo : TraitInfo, ILobbyOptions
{
	public const string Id = "ResearchLimit";

	[Desc("The maximum research level.")]
	public readonly int Limit;

	IEnumerable<LobbyOption> ILobbyOptions.LobbyOptions(MapPreview map)
	{
		var values = new Dictionary<string, string>();

		for (var i = 1; i <= this.Limit; i++)
			values.Add(i.ToString(), i.ToString());

		yield return new LobbyOption(
			map,
			ResearchLimitInfo.Id,
			"Research Limit",
			"Maximum research level.",
			true,
			0,
			new ReadOnlyDictionary<string, string>(values),
			this.Limit.ToString(),
			false
		);
	}

	public override object Create(ActorInitializer init)
	{
		return new ResearchLimit(this);
	}
}

public class ResearchLimit : INotifyCreated
{
	private readonly ResearchLimitInfo info;
	public int Limit { get; private set; }

	public ResearchLimit(ResearchLimitInfo info)
	{
		this.info = info;
	}

	void INotifyCreated.Created(Actor self)
	{
		this.Limit = int.Parse(self.World.LobbyInfo.GlobalSettings.OptionOrDefault(ResearchLimitInfo.Id, this.info.Limit.ToString()));
	}
}
