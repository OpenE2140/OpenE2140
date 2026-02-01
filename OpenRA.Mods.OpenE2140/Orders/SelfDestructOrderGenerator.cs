using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.OpenE2140.Traits.Buildings;

namespace OpenRA.Mods.OpenE2140.Orders;

public class SelfDestructOrderGenerator : GlobalButtonOrderGenerator<SelfDestructible>
{
	public SelfDestructOrderGenerator(World world)
		: base(world, SelfDestructible.SelfDestructOrderID) { }

	protected override string GetCursor(World world, CPos cell, int2 worldPixel, MouseInput mi)
	{
		mi.Button = MouseButton.Left;

		foreach (var item in this.OrderInner(world, cell, worldPixel, mi))
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
