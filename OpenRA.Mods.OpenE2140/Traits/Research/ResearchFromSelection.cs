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

using OpenRA.Mods.OpenE2140.Widgets.Logic;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.OpenE2140.Traits;

[TraitLocation(SystemActors.World)]
[Desc($"Makes it possible to switch to research tab, when actor with {nameof(Researches)} trait is selected. Attach to world actor.")]
public class ResearchFromSelectionInfo : TraitInfo
{
	[Desc($"Name root widget for research tab (i.e. the one with {nameof(ResearchLogic)} logic object.")]
	public readonly string ResearchWidget = string.Empty;

	public override object Create(ActorInitializer init) { return new ResearchFromSelection(init.World, this); }
}

public class ResearchFromSelection : INotifySelection
{
	private readonly World world;
	private readonly Lazy<Widget?> widget;

	public ResearchFromSelection(World world, ResearchFromSelectionInfo info)
	{
		this.world = world;

		this.widget = Exts.Lazy(() => (Widget?)Ui.Root.GetOrNull(info.ResearchWidget));
	}

	void INotifySelection.SelectionChanged()
	{
		if (this.world.LocalPlayer == null)
			return;

		var researches = this.world.Selection.Actors
			.Where(a => a.IsInWorld && a.World.LocalPlayer == a.Owner)
			.SelectMany(a => a.TraitsImplementing<Researches>())
			.FirstOrDefault(q => q.IsTraitEnabled());

		if (researches == null || this.widget.Value == null)
			return;

		var logic = this.widget.Value.LogicObjects.OfType<ResearchLogic>().FirstOrDefault();
		if (logic == null)
			return;

		logic.SwitchToResearch();
	}
}
