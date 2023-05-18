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
using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;
using EncyclopediaInfo = OpenRA.Mods.OpenE2140.Traits.EncyclopediaInfo;

namespace OpenRA.Mods.OpenE2140.Widgets.Logic;

[UsedImplicitly]
public class EncyclopediaLogic : ChromeLogic
{
	private readonly World world;
	private readonly ModData modData;

	private readonly ScrollPanelWidget descriptionPanel;
	private readonly LabelWidget descriptionLabel;
	private readonly SpriteFont descriptionFont;

	private readonly ScrollPanelWidget actorList;
	private readonly ScrollItemWidget headerTemplate;
	private readonly ScrollItemWidget template;
	private readonly ActorPreviewWidget previewWidget;
	private readonly LoopedVideoPlayerWidget animationWidget;

	private ActorInfo? selectedActor;
	private ScrollItemWidget? firstItem;

	[ObjectCreator.UseCtor]
	public EncyclopediaLogic(Widget widget, World world, ModData modData, Action onExit)
	{
		this.world = world;
		this.modData = modData;

		this.actorList = widget.Get<ScrollPanelWidget>("ACTOR_LIST");

		this.headerTemplate = widget.Get<ScrollItemWidget>("HEADER");
		this.template = widget.Get<ScrollItemWidget>("TEMPLATE");

		widget.Get("ACTOR_INFO").IsVisible = () => this.selectedActor != null;

		this.previewWidget = widget.Get<ActorPreviewWidget>("ACTOR_PREVIEW");
		this.previewWidget.IsVisible = () => this.selectedActor != null;

		this.animationWidget = widget.Get<LoopedVideoPlayerWidget>("ANIMATION");
		this.animationWidget.IsVisible = () => this.selectedActor != null;

		this.descriptionPanel = widget.Get<ScrollPanelWidget>("ACTOR_DESCRIPTION_PANEL");

		this.descriptionLabel = this.descriptionPanel.Get<LabelWidget>("ACTOR_DESCRIPTION");
		this.descriptionFont = Game.Renderer.Fonts[this.descriptionLabel.Font];

		this.actorList.RemoveChildren();

		var actorEncyclopediaPair = this.GetFilteredActorEncyclopediaPairs().ToArray();
		var categories = actorEncyclopediaPair.Select(a => a.Value.Category).Distinct().OrderBy(string.IsNullOrWhiteSpace).ThenBy(s => s);

		foreach (var category in categories)
			this.CreateActorGroup(category, actorEncyclopediaPair.Where(a => a.Value.Category == category).OrderBy(a => a.Value.Order).Select(a => a.Key));

		widget.Get<ButtonWidget>("BACK_BUTTON").OnClick = () =>
		{
			Game.Disconnect();
			Ui.CloseWindow();
			onExit();
		};
	}

	private IEnumerable<KeyValuePair<ActorInfo, EncyclopediaInfo>> GetFilteredActorEncyclopediaPairs()
	{
		var actors = new List<KeyValuePair<ActorInfo, EncyclopediaInfo>>();

		foreach (var actor in this.modData.DefaultRules.Actors.Values)
		{
			if (!actor.TraitInfos<IRenderActorPreviewSpritesInfo>().Any())
				continue;

			var statistics = actor.TraitInfoOrDefault<UpdatesPlayerStatisticsInfo>();

			if (statistics != null && !string.IsNullOrEmpty(statistics.OverrideActor))
				continue;

			var encyclopedia = actor.TraitInfoOrDefault<EncyclopediaInfo>();

			if (encyclopedia == null)
				continue;

			actors.Add(new KeyValuePair<ActorInfo, EncyclopediaInfo>(actor, encyclopedia));
		}

		return actors;
	}

	private void CreateActorGroup(string title, IEnumerable<ActorInfo> actors)
	{
		var header = ScrollItemWidget.Setup(this.headerTemplate, () => false, () => { });
		header.Get<LabelWidget>("LABEL").GetText = () => title;
		this.actorList.AddChild(header);

		foreach (var actor in actors)
		{
			var item = ScrollItemWidget.Setup(
				this.template,
				() => this.selectedActor != null && this.selectedActor.Name == actor.Name,
				() => this.SelectActor(actor)
			);

			var label = item.Get<LabelWithTooltipWidget>("TITLE");
			var name = actor.TraitInfoOrDefault<TooltipInfo>()?.Name;

			if (!string.IsNullOrEmpty(name))
				WidgetUtils.TruncateLabelToTooltip(label, name);

			if (this.firstItem == null)
			{
				this.firstItem = item;
				this.SelectActor(actor);
			}

			this.actorList.AddChild(item);
		}
	}

	private void SelectActor(ActorInfo actor)
	{
		this.selectedActor = actor;

		var info = actor.TraitInfoOrDefault<EncyclopediaInfo>();

		this.animationWidget.SetVideo(info?.Animation);

		var typeDictionary = new TypeDictionary
		{
			new OwnerInit(this.world.WorldActor.Owner), new FactionInit(this.world.WorldActor.Owner.PlayerReference.Faction)
		};

		foreach (var actorPreviewInit in actor.TraitInfos<IActorPreviewInitInfo>())
		foreach (var inits in actorPreviewInit.ActorPreviewInits(actor, ActorPreviewType.ColorPicker))
			typeDictionary.Add(inits);

		this.previewWidget.SetPreview(actor, typeDictionary);

		var text = string.Empty;

		if (info != null)
		{
			if (info.Title != null)
				text += $"{info.Title}\n\n";

			// TODO must come from an armor trait!
			if (info.Armor != null)
				text += $"Armor: {info.Armor}\n";

			// TODO must come from the armament trait!
			if (info.Armament != null)
				text += $"Armament: {info.Armament}\n";

			// TODO must come from the building trait!
			if (info.Resistance != null)
				text += $"Resistance: {info.Resistance}\n";
		}

		var valued = actor.TraitInfoOrDefault<ValuedInfo>();

		if (valued != null)
			text += $"Price: {valued.Cost}\n";

		var power = actor.TraitInfoOrDefault<PowerInfo>();

		if (power != null)
		{
			text += power.Amount switch
			{
				0 => "Energy usage: None - contains a power generator\n",
				> 0 => $"Energy supplied: {power.Amount} energy units\n",
				< 0 => $"Energy requirements: {power.Amount} energy units\n"
			};
		}

		if (text != string.Empty)
			text += "\n\n";

		if (info != null && !string.IsNullOrEmpty(info.Description))
			text += WidgetUtils.WrapText($"{info.Description.Replace("\\n", "\n").Trim()}\n", this.descriptionLabel.Bounds.Width, this.descriptionFont);

		var height = this.descriptionFont.Measure(text).Y;
		this.descriptionLabel.Text = text;
		this.descriptionLabel.Bounds.Height = height;
		this.descriptionPanel.Layout.AdjustChildren();

		this.descriptionPanel.ScrollToTop();
	}
}
