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
using OpenRA.Widgets;

namespace OpenRA.Mods.OpenE2140.Widgets.Logic;

[UsedImplicitly]
public class MainMenuLogic : Common.Widgets.Logic.MainMenuLogic
{
	[ObjectCreator.UseCtor]
	public MainMenuLogic(Widget widget, World world, ModData modData)
		: base(widget, world, modData)
	{
		var loadButton = widget.GetOrNull<ButtonWidget>("LOAD_BUTTON");

		var encyclopediaButton = (ButtonWidget)loadButton.Clone();
		encyclopediaButton.Id = "ENCYCLOPEDIA_BUTTON";
		encyclopediaButton.Y = new Support.IntegerExpression($"{encyclopediaButton.Y.Expression} + {loadButton.Height.Expression} + 10");
		encyclopediaButton.Text = "Encyclopedia";
		encyclopediaButton.GetText = () => encyclopediaButton.Text;

		encyclopediaButton.OnClick = () =>
			this.GetType().BaseType?.GetMethod("OpenEncyclopediaPanel", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(this, Array.Empty<object>());

		loadButton.Parent.AddChild(encyclopediaButton);

		encyclopediaButton.Initialize(new WidgetArgs());
	}
}
