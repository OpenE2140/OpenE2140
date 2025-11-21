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

using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.OpenE2140.Traits;

namespace OpenRA.Mods.OpenE2140.Orders
{
	public class SelfDestructOrderGenerator : GlobalButtonOrderGenerator<SelfDestructible>
	{
		public SelfDestructOrderGenerator()
			: base(SelfDestructible.SelfDestructOrderID) { }

		protected override string GetCursor(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			mi.Button = MouseButton.Left;

			foreach (var item in this.OrderInner(world, mi))
			{
				var selfDestructible = item.Subject.TraitOrDefault<SelfDestructible>();
				if (selfDestructible != null)
				{
					return item.Subject.IsDead ? selfDestructible.Info.BlockedCursor : selfDestructible.Info.Cursor;
				}
			}

			return "generic-blocked";
		}
	}
}

