using OpenRA.Activities;
using OpenRA.Mods.Common.Traits.Render;

namespace OpenRA.Mods.OpenE2140.Traits.Resources.Activities;

public class DelayCancelAnimation : Activity
{
	private readonly WithSpriteBody wsb;

	private int tickDelay;

	public DelayCancelAnimation(WithSpriteBody wsb, int delay)
	{
		this.wsb = wsb;
		this.tickDelay = delay;
		this.ChildHasPriority = false;
	}

	public override bool Tick(Actor self)
	{
		if (this.IsCanceling || --this.tickDelay == 0)
		{
			this.wsb.CancelCustomAnimation(self);
		}

		return this.TickChild(self);
	}
}
