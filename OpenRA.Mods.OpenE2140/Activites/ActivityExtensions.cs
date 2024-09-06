using OpenRA.Activities;

namespace OpenRA.Mods.OpenE2140.Activites;

public static class ActivityExtensions
{
	public static void TryQueueChild(this Activity activity, Activity? childActivity)
	{
		if (childActivity == null)
			return;

		activity.QueueChild(childActivity);
	}
}
