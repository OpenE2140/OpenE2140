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

using System.Reflection;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.OpenE2140.Widgets.Logic;

public class AddFactionSuffixExLogic : ChromeLogic
{
	[ObjectCreator.UseCtor]
	public AddFactionSuffixExLogic(Widget widget, World world)
	{
		if (world.LocalPlayer == null || world.LocalPlayer.Spectating)
			return;

		if (!ChromeMetrics.TryGet("FactionSuffix-" + world.LocalPlayer.Faction.InternalName, out string faction))
			faction = world.LocalPlayer.Faction.InternalName;

		var suffix = "-" + faction;

		if (widget is IFactionSpecificWidget fsw)
		{
			foreach (var fieldName in fsw.FieldsToOverride)
			{
				var fieldInfo = fsw.GetType().GetField(fieldName, BindingFlags.Default | BindingFlags.Public | BindingFlags.Instance);

				if (fieldInfo is null)
					throw new InvalidOperationException($"Widget {fsw.GetType().Name} does not have field {fieldName}.");

				if (ChromeMetricsHelper.TryGet(fieldInfo.FieldType, $"{fsw.Identifier}{fieldName}{suffix}", out var result)
					|| ChromeMetricsHelper.TryGet(fieldInfo.FieldType, $"{fsw.Identifier}{fieldName}", out result))
					fieldInfo.SetValue(fsw, result);
			}
		}
		else if (widget is ProductionPaletteWidget ppw)
			ppw.Parent.Get<BackgroundWidget>("ICON_TEMPLATE").Background += suffix;
		else if (widget is ProductionTabsExWidget ptw)
		{
			if (ptw.ArrowButton != null)
				ptw.ArrowButton += suffix;

			if (ptw.TabButton != null)
				ptw.TabButton += suffix;

			ptw.Decorations += suffix;
			ptw.RefreshCaches();
		}
		else
		{
			Log.Write(
				"debug",
				"AddFactionSuffixExLogic only supports IFactionSpecificWidget, ProductionPaletteWidget and ProductionTabsExWidget. "
				+ $"Type: {widget.GetType().FullName}"
			);
		}
	}

	private static class ChromeMetricsHelper
	{
		private static readonly MethodInfo TryGetMethod = typeof(ChromeMetrics).GetMethod(
			nameof(ChromeMetrics.TryGet),
			BindingFlags.Default | BindingFlags.Static | BindingFlags.Public
		)!;

		private static readonly Cache<Type, Func<string, (bool, object?)>> Cache =
			new Cache<Type, Func<string, (bool, object?)>>(ChromeMetricsHelper.TryGetCore);

		public static bool TryGet(Type type, string key, out object? result)
		{
			var getter = ChromeMetricsHelper.Cache[type];
			(var success, result) = getter.Invoke(key);

			return success;
		}

		private static Func<string, (bool, object?)> TryGetCore(Type type)
		{
			return key =>
			{
				var args = new object?[] { key, null };
				var success = (bool)ChromeMetricsHelper.TryGetMethod.MakeGenericMethod(type).Invoke(null, args)!;

				return (success, args[1]);
			};
		}
	}
}
