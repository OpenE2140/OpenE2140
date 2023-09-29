﻿using OpenRA.Mods.OpenE2140.Widgets.Logic;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.OpenE2140.Traits.Research;

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
	private readonly OpenRA.World world;
	private readonly Lazy<Widget?> widget;

	public ResearchFromSelection(OpenRA.World world, ResearchFromSelectionInfo info)
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
