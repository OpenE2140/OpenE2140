using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Mods.OpenE2140.Orders;
using OpenRA.Widgets;

namespace OpenRA.Mods.OpenE2140.Widgets.Logic;

public class SelfDestructOrderButtonLogic : ChromeOrderButtonLogic<SelfDestructOrderGenerator>
{
	[ObjectCreator.UseCtor]
	public SelfDestructOrderButtonLogic(Widget widget, World world)
		: base(widget, world, "selfdestruct")
	{
	}
}
