using OpenRA.Activities;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits.Render;

namespace OpenRA.Mods.OpenE2140.Traits.Resources.Activities;

public class PlayAnimation : Activity
{
	private readonly string sequenceName;
	private readonly Action? continuationCallback;
	private readonly WithSpriteBody wsb;

	public bool ResetToIdleAfterFinish { get; init; } = false;
	public int Delay { get; init; } = 0;

	private bool startedPlaying;
	private bool isPlaying;

	public PlayAnimation(Actor self, string sequenceName, Action? continuationCallback = null)
	{
		this.sequenceName = sequenceName;
		this.continuationCallback = continuationCallback;
		this.wsb = self.Trait<WithSpriteBody>();
	}

	protected override void OnFirstRun(Actor self)
	{
		if (this.Delay > 0)
		{
			this.QueueChild(new Wait(this.Delay));
		}
	}

	public override bool Tick(Actor self)
	{
		if (this.IsCanceling)
		{
			this.wsb.CancelCustomAnimation(self);

			return true;
		}

		if (!this.startedPlaying)
		{
			this.wsb.DefaultAnimation.PlayThen(this.wsb.NormalizeSequence(self, this.sequenceName), () =>
			{
				if (this.ResetToIdleAfterFinish)
					this.wsb.CancelCustomAnimation(self);

				this.isPlaying = false;
			});

			this.startedPlaying = true;
			this.isPlaying = true;
		}

		return !this.isPlaying;
	}

	protected override void OnLastRun(Actor self)
	{
		if (!this.IsCanceling)
			this.continuationCallback?.Invoke();
	}
}
