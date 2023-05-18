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
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.OpenE2140.Helpers.Reflection;
using OpenRA.Network;
using OpenRA.Widgets;

namespace OpenRA.Mods.OpenE2140.Widgets;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class ProductionPaletteExWidget : ProductionPaletteWidget, IFactionSpecificWidget
{
	private readonly World world;
	private readonly ObjectFieldHelper<string> clockAnimationField;

	public readonly string Identifier = string.Empty;

	[ObjectCreator.UseCtor]
	public ProductionPaletteExWidget(ModData modData, OrderManager orderManager, World world, WorldRenderer worldRenderer)
		: base(modData, orderManager, world, worldRenderer)
	{
		this.world = world;
		this.clockAnimationField = ReflectionHelper.GetFieldHelper(this, this.clockAnimationField, nameof(ProductionPaletteWidget.ClockAnimation));
	}

	public override void Initialize(WidgetArgs args)
	{
		if (this.world.LocalPlayer?.Spectating == false)
		{
			var localPlayerFaction = this.world.LocalPlayer.Faction.InternalName;

			this.clockAnimationField.Value = $"{this.ClockAnimation}-{localPlayerFaction}";
		}

		base.Initialize(args);
	}

	string[] IFactionSpecificWidget.FieldsToOverride => new[] { nameof(this.TextColor), nameof(this.ReadyTextAltColor) };

	string IFactionSpecificWidget.Identifier => this.Identifier;
}
