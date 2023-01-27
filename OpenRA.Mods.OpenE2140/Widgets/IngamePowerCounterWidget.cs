using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;

namespace OpenRA.Mods.OpenE2140.Widgets
{
	public class IngamePowerWidget : WorldLabelWithTooltipWidget
	{
		public Color NormalPowerColor = Color.White;
		public Color CriticalPowerColor = Color.Red;

		[ObjectCreator.UseCtor]
		public IngamePowerWidget(World world)
			: base(world)
		{
		}
	}
}
