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
using OpenRA.Primitives;
using OpenRA.Widgets;
using PowerInfo = OpenRA.Mods.OpenE2140.Traits.Power.PowerInfo;
using PowerManager = OpenRA.Mods.OpenE2140.Traits.Power.PowerManager;

namespace OpenRA.Mods.OpenE2140.Widgets.Logic;

[UsedImplicitly]
public class ProductionTooltipLogic : ChromeLogic
{
	[ObjectCreator.UseCtor]
	public ProductionTooltipLogic(Widget widget, TooltipContainerWidget tooltipContainer, Player player, Func<ProductionIcon?> getTooltipIcon)
	{
		foreach (var id in new[] { "COST_ICON", "TIME_ICON", "POWER_ICON" })
			new AddFactionSuffixLogic(widget.Get<Widget>(id), player.World);

		var world = player.World;
		var pm = player.PlayerActor.TraitOrDefault<PowerManager>();
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

		var padding = nameLabel.Bounds.Location;
		var iconWidth = timeIcon.Bounds.Width;

		tooltipContainer.BeforeRender = () =>
		{
			var tooltipIcon = getTooltipIcon();
			var actor = tooltipIcon?.Actor;

			if (tooltipIcon == null || actor == null)
				return;

			// Fetch data
			var transforms = actor.TraitInfos<TransformsInfo>().FirstOrDefault();
			var cost = actor.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0;
			var hotkey = tooltipIcon.Hotkey?.GetValue() ?? Hotkey.Invalid;
			var buildable = actor.TraitInfo<BuildableInfo>();

			if (transforms != null)
				actor = player.World.Map.Rules.Actors[transforms.IntoActor];

			var tooltip = actor.TraitInfos<TooltipInfo>().FirstOrDefault();
			var power = actor.TraitInfos<PowerInfo>().Sum(i => i.Amount);

			// Name
			nameLabel.Text = tooltip?.Name ?? actor.Name;
			var nameSize = font.Measure(nameLabel.Text);
			nameLabel.Bounds = new Rectangle(padding.X, padding.Y, nameSize.X, nameSize.Y);

			// Hotkey
			hotkeyLabel.Visible = hotkey.IsValid();
			hotkeyLabel.Bounds = new Rectangle(nameLabel.Bounds.Right + padding.X, nameLabel.Bounds.Y, 0, 0);

			if (hotkeyLabel.Visible)
			{
				hotkeyLabel.Text = $"({hotkey.DisplayString()})";
				var hotkeySize = font.Measure(hotkeyLabel.Text);
				hotkeyLabel.Bounds.Width = hotkeySize.X;
				hotkeyLabel.Bounds.Height = hotkeySize.Y;
			}

			// Requires
			// TODO Implement research
			requiresLabel.Visible = false;
			requiresLabel.Bounds = new Rectangle(nameLabel.Bounds.X, nameLabel.Bounds.Bottom + padding.Y, 0, 0);

			if (requiresLabel.Visible)
			{
				requiresLabel.Text = "UNIMPLEMENTED";
				var requiredSize = font.Measure(hotkeyLabel.Text);
				requiresLabel.Bounds.Width = requiredSize.X;
				requiresLabel.Bounds.Height = requiredSize.Y;
			}

			// Description
			descLabel.Visible = buildable.Description != string.Empty;
			descLabel.Bounds = new Rectangle(requiresLabel.Bounds.X, requiresLabel.Bounds.Bottom + padding.Y, 0, 0);

			if (descLabel.Visible)
			{
				descLabel.Text = buildable.Description.Replace("\\n", "\n");
				var descSize = descFont.Measure(descLabel.Text);
				descLabel.Bounds.Width = descSize.X;
				descLabel.Bounds.Height = descSize.Y;
			}

			var iconOffset = new[] { hotkeyLabel.Bounds.Right, requiresLabel.Bounds.Right, descLabel.Bounds.Right }.Aggregate(Math.Max) + padding.X;
			var labelOffset = iconOffset + iconWidth + padding.X;

			powerIcon.Bounds.X = timeIcon.Bounds.X = costIcon.Bounds.X = iconOffset;

			// Cost
			costIcon.Visible = costLabel.Visible = cost != 0;
			costLabel.Bounds = new Rectangle(labelOffset, padding.Y, 0, 0);
			costIcon.Bounds.Y = costLabel.Bounds.Y;

			if (costLabel.Visible)
			{
				costLabel.Text = cost.ToString();
				var costSize = font.Measure(costLabel.Text);

				costLabel.GetColor = () => cost < 0 ? Color.Green :
					cost > pr.Cash + pr.Resources ? Color.Red : Color.White;

				costLabel.Bounds.Width = costSize.X;
				costLabel.Bounds.Height = costSize.Y;
			}

			// Time
			timeIcon.Visible = timeLabel.Visible = buildable.BuildDuration != 0;
			timeLabel.Bounds = new Rectangle(labelOffset, costLabel.Bounds.Bottom + padding.Y, 0, 0);
			timeIcon.Bounds.Y = timeLabel.Bounds.Y;

			if (timeLabel.Visible)
			{
				timeLabel.Text = formatBuildTime.Update(buildable.BuildDuration);
				var timeSize = font.Measure(timeLabel.Text);
				timeLabel.Bounds.Width = timeSize.X;
				timeLabel.Bounds.Height = timeSize.Y;
			}

			// Power
			powerIcon.Visible = powerLabel.Visible = power != 0;
			powerLabel.Bounds = new Rectangle(labelOffset, timeLabel.Bounds.Bottom + padding.Y, 0, 0);
			powerIcon.Bounds.Y = powerLabel.Bounds.Y;

			if (powerLabel.Visible)
			{
				powerLabel.Text = power.ToString();
				var powerSize = font.Measure(powerLabel.Text);

				powerLabel.GetColor = () => power > 0 ? Color.Green :
					pm == null || power > pm.Power ? Color.Red : Color.White;

				powerLabel.Bounds.Width = powerSize.X;
				powerLabel.Bounds.Height = powerSize.Y;
			}

			var width = new[] { costLabel.Bounds.Right, requiresLabel.Bounds.Right, timeLabel.Bounds.Right, powerLabel.Bounds.Right }.Aggregate(Math.Max);
			var height = new[] { descLabel.Bounds.Bottom, powerLabel.Bounds.Bottom }.Aggregate(Math.Max);

			widget.Bounds.Width = width + padding.X;
			widget.Bounds.Height = height + padding.Y;
		};
	}
}
