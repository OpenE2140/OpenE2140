#region Copyright & License Information

/*
 * Copyright 2007-2023 The OpenE2140 Developers (see AUTHORS)
 * This file is part of OpenE2140, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */

#endregion

using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;

namespace OpenRA.Mods.OpenE2140.Widgets;

public class IngamePowerWidget : WorldLabelWithTooltipWidget, IFactionSpecificWidget
{
	public Color NormalPowerColor = Color.White;
	public Color CriticalPowerColor = Color.Red;

	public string Identifier = "";

	[ObjectCreator.UseCtorAttribute]
	public IngamePowerWidget(World world)
		: base(world)
	{
	}

	string[] IFactionSpecificWidget.FieldsToOverride => new[] { nameof(this.NormalPowerColor), nameof(this.CriticalPowerColor), nameof(LabelWidget.TextColor) };

	string IFactionSpecificWidget.Identifier => this.Identifier;
}
