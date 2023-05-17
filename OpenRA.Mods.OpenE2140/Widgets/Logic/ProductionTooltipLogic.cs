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
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Widgets;

namespace OpenRA.Mods.OpenE2140.Widgets.Logic;

[UsedImplicitly]
public class ProductionTooltipLogic : Common.Widgets.Logic.ProductionTooltipLogic
{
	[ObjectCreator.UseCtor]
	public ProductionTooltipLogic(Widget widget, TooltipContainerWidget tooltipContainer, Player player, Func<ProductionIcon> getTooltipIcon)
		: base(widget, tooltipContainer, player, getTooltipIcon)
	{
		foreach (var id in new[] { "COST_ICON", "TIME_ICON", "POWER_ICON" })
			new AddFactionSuffixLogic(widget.Get<Widget>(id), player.World);
	}
}
