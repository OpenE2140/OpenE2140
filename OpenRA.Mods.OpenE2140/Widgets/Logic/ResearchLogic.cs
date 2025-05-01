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

using System.Reflection;
using JetBrains.Annotations;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.OpenE2140.Traits.Research;
using OpenRA.Widgets;

namespace OpenRA.Mods.OpenE2140.Widgets.Logic;

[UsedImplicitly]
public class ResearchLogic : ChromeLogic
{
	private readonly World world;

	private readonly ProductionTabsWidget tabs;
	private readonly ProductionPaletteExWidget production;
	private readonly ResearchPaletteWidget research;
	private readonly WorldButtonWidget button;

	private Researches[] researchers = [];
	private int icons;

	[ObjectCreator.UseCtor]
	public ResearchLogic(Widget widget, World world)
	{
		this.world = world;

		this.tabs = widget.Get<ProductionTabsWidget>("PRODUCTION_TABS");
		this.production = widget.Get<ProductionPaletteExWidget>("PRODUCTION_PALETTE");
		this.research = widget.Get<ResearchPaletteWidget>("RESEARCH_PALETTE");

		this.button = Ui.Root.Get(this.tabs.TypesContainer).Get<WorldButtonWidget>("RESEARCH");

		this.button.IsDisabled = () => this.researchers.Length == 0;
		this.button.OnMouseUp = _ => this.SwitchToResearch();
		this.button.OnKeyPress = _ => this.SwitchToResearch();
		this.button.IsHighlighted = () => this.tabs.QueueGroup == null && this.research.Visible;

		this.button.Get<ImageWidget>("ICON").GetImageName = () => this.button.IsDisabled() ? "research-disabled" : "research";
	}

	public override void Tick()
	{
		this.researchers = this.world.ActorsWithTrait<Researches>()
			.Where(e => e.Actor.Owner == this.world.LocalPlayer && !e.Trait.IsTraitDisabled)
			.Select(e => e.Trait)
			.ToArray();

		if (!this.research.Visible)
			return;

		if (this.tabs.QueueGroup != null || this.button.IsDisabled())
			this.SwitchToProduction();
		else if (this.icons != this.research.Researchables.Length)
		{
			this.UpdateBackgrounds(this.icons, this.research.Researchables.Length);
			this.icons = this.research.Researchables.Length;
		}
	}

	public void SwitchToResearch()
	{
		if (this.research.Visible)
			return;

		// Disable production tab icon.
		this.tabs.QueueGroup = null;

		this.research.Visible = true;
		this.production.Visible = false;

		this.icons = this.research.Researchables.Length;

		this.UpdateBackgrounds(this.production.DisplayedIconCount, this.research.Researchables.Length);
	}

	private void SwitchToProduction()
	{
		this.research.Visible = false;
		this.production.Visible = true;

		this.UpdateBackgrounds(this.research.Researchables.Length, this.production.DisplayedIconCount);
	}

	private void UpdateBackgrounds(int oldAmount, int newAmount)
	{
		(typeof(ProductionPaletteWidget).GetField(nameof(ProductionPaletteWidget.OnIconCountChanged), BindingFlags.Instance | BindingFlags.NonPublic)
			?.GetValue(this.production) as Delegate)?.DynamicInvoke(oldAmount, newAmount);
	}
}
