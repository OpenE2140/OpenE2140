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
using OpenRA.Primitives;

namespace OpenRA.Mods.OpenE2140.Widgets;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class IngamePowerWidget : WorldLabelWithTooltipWidget, IFactionSpecificWidget
{
	public readonly Color NormalPowerColor = Color.White;
	public readonly Color CriticalPowerColor = Color.Red;

	public readonly string Identifier = string.Empty;

	[ObjectCreator.UseCtor]
	public IngamePowerWidget(ModData modData, World world)
		: base(modData, world)
	{
	}

	string[] IFactionSpecificWidget.FieldsToOverride => [nameof(this.NormalPowerColor), nameof(this.CriticalPowerColor), nameof(LabelWidget.TextColor)];

	string IFactionSpecificWidget.Identifier => this.Identifier;
}
