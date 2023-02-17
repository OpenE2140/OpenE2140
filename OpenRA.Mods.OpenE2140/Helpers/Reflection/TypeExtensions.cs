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

namespace OpenRA.Mods.OpenE2140.Helpers.Reflection;

public static class TypeExtensions
{
	public static IEnumerable<FieldInfo> GetAllInstanceFields(this Type type)
	{
		if (type is null)
			throw new ArgumentNullException(nameof(type));

		foreach (var item in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
			yield return item;

		var currentType = type.BaseType;

		while (currentType != null && currentType != typeof(object))
		{
			foreach (var item in currentType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
			{
				// Ignore non-private fields
				if (item.IsPrivate)
					yield return item;
			}

			currentType = currentType.BaseType;
		}
	}
}
