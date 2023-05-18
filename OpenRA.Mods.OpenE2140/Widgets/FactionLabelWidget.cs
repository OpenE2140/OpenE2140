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

namespace OpenRA.Mods.OpenE2140.Widgets;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class FactionLabelWidget : WorldLabelWithTooltipWidget, IFactionSpecificWidget
{
	public readonly string[] FieldsToOverride = { "TextColor" };

	[FieldLoader.Require]
	public readonly string Identifier = string.Empty;

	[ObjectCreator.UseCtor]
	public FactionLabelWidget(World world)
		: base(world)
	{
	}

	string[] IFactionSpecificWidget.FieldsToOverride => this.FieldsToOverride;

	string IFactionSpecificWidget.Identifier => this.Identifier;
}
