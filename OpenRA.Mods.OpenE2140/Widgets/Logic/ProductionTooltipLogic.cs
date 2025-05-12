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
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Mods.OpenE2140.Traits.Mcu;
using OpenRA.Mods.OpenE2140.Traits.Power;
using OpenRA.Mods.OpenE2140.Traits.Research;
using OpenRA.Mods.OpenE2140.Traits.WaterBase;
using OpenRA.Primitives;
using OpenRA.Widgets;
using PowerInfo = OpenRA.Mods.OpenE2140.Traits.Power.PowerInfo;

namespace OpenRA.Mods.OpenE2140.Widgets.Logic;

[UsedImplicitly]
public class ProductionTooltipLogic : ChromeLogic
{
	[ObjectCreator.UseCtor]
	public ProductionTooltipLogic(Widget widget, TooltipContainerWidget tooltipContainer, Player player, Func<ProductionIcon?> getTooltipIcon)
	{
		foreach (var id in new[] { "COST_ICON", "TIME_ICON", "POWER_ICON" })
			new AddPlayerFactionSuffixLogic(widget.Get<Widget>(id), player);

		var world = player.World;
		var pm = player.PlayerActor.TraitOrDefault<PowerManagerBase>();
		var pr = player.PlayerActor.Trait<PlayerResources>();

		widget.IsVisible = () => getTooltipIcon() != null && getTooltipIcon()?.Actor != null;

		var nameLabel = widget.Get<LabelWidget>("NAME");
		var hotkeyLabel = widget.Get<LabelWidget>("HOTKEY");
		var requiresLabel = widget.Get<LabelWidget>("REQUIRES");
		var powerLabel = widget.Get<LabelWidget>("POWER");
		var powerIcon = widget.Get<ImageWidget>("POWER_ICON");
		var timeLabel = widget.Get<LabelWidget>("TIME");
		var timeIcon = widget.Get<ImageWidget>("TIME_ICON");
		var costLabel = widget.Get<LabelWidget>("COST");
		var costIcon = widget.Get<ImageWidget>("COST_ICON");
		var descLabel = widget.Get<LabelWidget>("DESC");

		var font = Game.Renderer.Fonts[nameLabel.Font];
		var descFont = Game.Renderer.Fonts[descLabel.Font];
		var formatBuildTime = new CachedTransform<int, string>(time => WidgetUtils.FormatTime(time, world.Timestep));

		var padding = nameLabel.Bounds.ToRectangle().Location;
		var iconWidth = timeIcon.Bounds.Width;

		tooltipContainer.BeforeRender = () =>
		{
			var tooltipIcon = getTooltipIcon();
			var actor = tooltipIcon?.Actor;

			if (tooltipIcon == null || actor == null)
				return;

			// Fetch data
			var cost = 0;

			if (tooltipIcon.ProductionQueue != null)
				cost = tooltipIcon.ProductionQueue.GetProductionCost(actor);
			else
			{
				var valued = actor.TraitInfoOrDefault<ValuedInfo>();

				if (valued != null)
					cost = valued.Cost;
			}

			var hotkey = tooltipIcon.Hotkey?.GetValue() ?? Hotkey.Invalid;
			var buildable = actor.TraitInfo<BuildableInfo>();

			var power = 0;
			if (McuUtils.TryGetTargetBuilding(player.World, actor, out var targetBuilding))
			{
				power = GetPowerRequirements(player.World, actor, targetBuilding);
				actor = targetBuilding;
			}

			var tooltip = actor.TraitInfos<TooltipInfo>().FirstOrDefault();

			var missingResearch = player.PlayerActor.TraitsImplementing<Researchable>()
				.Where(researchable => researchable.RemainingDuration != 0 && buildable.Prerequisites.Contains(researchable.Info.Id))
				.Select(researchable => researchable.Info.Name)
				.ToArray();

			var x = padding.X;
			var y = padding.Y;
			var x2 = x;

			// Name
			nameLabel.Text = tooltip?.Name ?? actor.Name;

			var nameSize = font.Measure(nameLabel.Text);
			nameLabel.Bounds = new WidgetBounds(x, y, nameSize.X, nameSize.Y);

			y += nameLabel.Bounds.Height + padding.Y;
			x2 = Math.Max(x2, nameLabel.Bounds.Right);

			// Hotkey
			hotkeyLabel.Visible = hotkey.IsValid();

			if (hotkeyLabel.Visible)
			{
				hotkeyLabel.Text = $"({hotkey.DisplayString()})";

				var hotkeySize = font.Measure(hotkeyLabel.Text);
				hotkeyLabel.Bounds = new WidgetBounds(nameLabel.Bounds.Right + padding.X, nameLabel.Bounds.Y, hotkeySize.X, hotkeySize.Y);

				x2 = Math.Max(x2, hotkeyLabel.Bounds.Right);
			}

			// Requires
			requiresLabel.Visible = missingResearch.Length > 0;

			if (requiresLabel.Visible)
			{
				requiresLabel.Text = string.Join(", ", missingResearch);
				requiresLabel.TextColor = Color.Red;

				var requiredSize = font.Measure(requiresLabel.Text);
				requiresLabel.Bounds = new WidgetBounds(x, y, requiredSize.X, requiredSize.Y);

				y += requiresLabel.Bounds.Height + padding.Y;
				x2 = Math.Max(x2, requiresLabel.Bounds.Right);
			}

			// Description
			descLabel.Visible = !string.IsNullOrEmpty(buildable.Description);

			if (descLabel.Visible)
			{
				descLabel.Text = buildable.Description.Replace("\\n", "\n");

				var descSize = descFont.Measure(descLabel.Text);
				descLabel.Bounds = new WidgetBounds(x, y, descSize.X, descSize.Y);

				y += descLabel.Bounds.Height + padding.Y;
				x2 = Math.Max(x2, descLabel.Bounds.Right);
			}

			var y2 = y;
			x = x2 + padding.X;
			y = padding.Y;
			var x3 = x + iconWidth + padding.X;

			// Cost
			costIcon.Visible = costLabel.Visible = cost != 0;

			if (costIcon.Visible)
			{
				costLabel.Text = cost.ToString();

				costLabel.GetColor = () => cost < 0 ? Color.Green :
					cost > pr.Cash + pr.Resources ? Color.Red : Color.White;

				costIcon.Bounds.X = x;
				costIcon.Bounds.Y = y;

				var costSize = font.Measure(costLabel.Text);
				costLabel.Bounds = new WidgetBounds(x3, y, costSize.X, costSize.Y);

				y += costLabel.Bounds.Height + padding.Y;
				x2 = Math.Max(x2, costLabel.Bounds.Right);
			}

			// Time
			timeIcon.Visible = timeLabel.Visible = buildable.BuildDuration != 0;

			if (timeLabel.Visible)
			{
				timeLabel.Text = formatBuildTime.Update(buildable.BuildDuration);

				timeIcon.Bounds.X = x;
				timeIcon.Bounds.Y = y;

				var timeSize = font.Measure(timeLabel.Text);
				timeLabel.Bounds = new WidgetBounds(x3, y, timeSize.X, timeSize.Y);

				y += timeLabel.Bounds.Height + padding.Y;
				x2 = Math.Max(x2, timeLabel.Bounds.Right);
			}

			// Power
			powerIcon.Visible = powerLabel.Visible = power != 0;

			if (powerLabel.Visible)
			{
				powerLabel.Text = power.ToString();

				powerLabel.GetColor = () => power > 0 ? Color.Green :
					pm == null || -power > pm.Power ? Color.Red : Color.White;

				powerIcon.Bounds.X = x;
				powerIcon.Bounds.Y = y;

				var powerSize = font.Measure(powerLabel.Text);
				powerLabel.Bounds = new WidgetBounds(x3, y, powerSize.X, powerSize.Y);

				y += powerLabel.Bounds.Height + padding.Y;
				x2 = Math.Max(x2, powerLabel.Bounds.Right);
			}

			widget.Bounds.Width = x2 + padding.X;
			widget.Bounds.Height = Math.Max(y2, y);
		};
	}

	private static int GetPowerRequirements(World world, ActorInfo mcuActor, ActorInfo buildingActor)
	{
		// Special handling for Water Base: it's the dock that produces naval actors and requires power.
		if (mcuActor.TryGetTrait<WaterBaseTransformsInfo>(out var waterBaseTransformsInfo))
			buildingActor = world.Map.Rules.Actors[waterBaseTransformsInfo.DockActor];

		return buildingActor.TraitInfos<PowerInfo>().Sum(i => i.Amount);
	}
}
