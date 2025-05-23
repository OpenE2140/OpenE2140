using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Mods.OpenE2140.Orders;
using OpenRA.Widgets;

namespace OpenRA.Mods.OpenE2140.Widgets.Logic;

public class SelfDestructOrderButtonLogic : ChromeLogic
{
	[ObjectCreator.UseCtor]
	public SelfDestructOrderButtonLogic(Widget widget, World world)
	{
		if (widget is ButtonWidget beacon)
			OrderButtonsChromeUtils.BindOrderButton<SelfDestructOrderGenerator>(world, beacon, "selfdestruct");
	}
}
