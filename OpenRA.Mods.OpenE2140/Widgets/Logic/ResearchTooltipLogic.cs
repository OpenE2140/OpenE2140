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
using OpenRA.Mods.OpenE2140.Traits;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.OpenE2140.Widgets.Logic
{
	[UsedImplicitly]
	public class ResearchTooltipLogic : ChromeLogic
	{
		[ObjectCreator.UseCtor]
		public ResearchTooltipLogic(Widget widget, TooltipContainerWidget tooltipContainer, Player player, Func<ResearchIcon?> getTooltipIcon)
		{
			foreach (var id in new[] { "COST_ICON", "TIME_ICON" })
				_ = new AddFactionSuffixLogic(widget.Get<Widget>(id), player.World);

			var world = player.World;
			var pr = player.PlayerActor.Trait<PlayerResources>();

			widget.IsVisible = () => getTooltipIcon() != null && getTooltipIcon()?.Researchable != null;

			var nameLabel = widget.Get<LabelWidget>("NAME");
			var hotkeyLabel = widget.Get<LabelWidget>("HOTKEY");
			var requiresLabel = widget.Get<LabelWidget>("REQUIRES");
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
				var researchable = tooltipIcon?.Researchable;

				if (tooltipIcon == null || researchable == null)
					return;

				// Fetch data
				var hotkey = tooltipIcon.Hotkey?.GetValue() ?? Hotkey.Invalid;

				var time = researchable.RemainingDuration
					/ Math.Max(player.World.ActorsWithTrait<Researches>().Count(e => e.Actor.Owner == player && !e.Trait.IsTraitDisabled), 1);

				var missingResearch = player.PlayerActor.Trait<Research>()
					.GetMissingRequirements(researchable)
					.Select(researchable => researchable.Info.Name)
					.ToArray();

				var x = padding.X;
				var y = padding.Y;
				var x2 = x;

				// Name
				nameLabel.Text = researchable.Info.Name;

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
					requiresLabel.Text = missingResearch[^1];
					requiresLabel.TextColor = Color.Red;

					var requiredSize = font.Measure(requiresLabel.Text);
					requiresLabel.Bounds = new WidgetBounds(x, y, requiredSize.X, requiredSize.Y);

					y += requiresLabel.Bounds.Height + padding.Y;
					x2 = Math.Max(x2, requiresLabel.Bounds.Right);
				}

				// Description
				descLabel.Visible = researchable.Info.Description != string.Empty;

				if (descLabel.Visible)
				{
					descLabel.Text = researchable.Info.Description.Replace("\\n", "\n");

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
				costIcon.Visible = costLabel.Visible = researchable.Info.Cost != 0;

				if (costIcon.Visible)
				{
					costLabel.Text = researchable.Info.Cost.ToString();

					costLabel.GetColor = () => researchable.Info.Cost < 0 ? Color.Green :
						researchable.Info.Cost > pr.Cash + pr.Resources ? Color.Red : Color.White;

					costIcon.Bounds.X = x;
					costIcon.Bounds.Y = y;

					var costSize = font.Measure(costLabel.Text);
					costLabel.Bounds = new WidgetBounds(x3, y, costSize.X, costSize.Y);

					y += costLabel.Bounds.Height + padding.Y;
					x2 = Math.Max(x2, costLabel.Bounds.Right);
				}

				// Time
				timeIcon.Visible = timeLabel.Visible = time != 0;

				if (timeLabel.Visible)
				{
					timeLabel.Text = formatBuildTime.Update(time);

					timeIcon.Bounds.X = x;
					timeIcon.Bounds.Y = y;

					var timeSize = font.Measure(timeLabel.Text);
					timeLabel.Bounds = new WidgetBounds(x3, y, timeSize.X, timeSize.Y);

					y += timeLabel.Bounds.Height + padding.Y;
					x2 = Math.Max(x2, timeLabel.Bounds.Right);
				}

				widget.Bounds.Width = x2 + padding.X;
				widget.Bounds.Height = Math.Max(y2, y);
			};
		}
	}
}

