using OpenRA.Activities;

namespace OpenRA.Mods.OpenE2140.Extensions;

public static class ActivityExtensions
{
	public static Activity WithChild(this Activity parent, Activity child)
	{
		parent.QueueChild(child);
		return parent;
	}

	public static Activity WithNext(this Activity current, Activity next)
	{
		current.Queue(next);
		return current;
	}
}
