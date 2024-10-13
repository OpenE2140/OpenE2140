using OpenRA.Activities;
using OpenRA.Mods.OpenE2140.Traits.Resources.Activities;

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

public class ConveyorBeltDockInfo : SharedDockHostInfo
{
	public static CrateMoveSequence DefaultLoadSequence = new CrateMoveSequence(new[] { 0, 3, 5, 2, 2 }, new[] { 400, 250, 150, 20, 0 });
	public static CrateMoveSequence DefaultUnloadSequence = new CrateMoveSequence(new[] { 0, 2, 2, 5, 3 }, new[] { 0, 20, 150, 250, 400 });

	// TODO: use just single sequence definition (for loading), generate unloading sequence from the loading

	[Desc("Sequence for crate movement during load animation")]
	[FieldLoader.LoadUsing(nameof(LoadLoadCrateMoveSequence))]
	public readonly CrateMoveSequence LoadSequence = DefaultLoadSequence;

	[Desc("Sequence for crate movement during unload animation")]
	[FieldLoader.LoadUsing(nameof(LoadUnloadCrateMoveSequence))]
	public readonly CrateMoveSequence UnloadSequence = DefaultUnloadSequence;

	private static object LoadLoadCrateMoveSequence(MiniYaml parentNode)
	{
		return LoadCrateMoveSequence(parentNode, nameof(LoadSequence)) ?? DefaultLoadSequence;
	}

	private static object LoadUnloadCrateMoveSequence(MiniYaml parentNode)
	{
		return LoadCrateMoveSequence(parentNode, nameof(UnloadSequence)) ?? DefaultUnloadSequence;
	}

	private static object? LoadCrateMoveSequence(MiniYaml parentNode, string key)
	{
		var node = parentNode.NodeWithKeyOrDefault(key);
		if (node == null)
			return null;

		return FieldLoader.Load<CrateMoveSequence>(node.Value);
	}

	public override object Create(ActorInitializer init)
	{
		return new ConveyorBeltDock(init.Self, this);
	}
}

public class CrateMoveSequence
{
	public readonly int[] Delays = Array.Empty<int>();
	public readonly int[] Offsets = Array.Empty<int>();

	public CrateMoveSequence()
	{
	}
	public CrateMoveSequence(int[] delays, int[] offsets)
	{
		this.Delays = delays;
		this.Offsets = offsets;
	}
}

public class ConveyorBeltDock : SharedDockHost, IConveyorBeltDockHost
{
	public ConveyorBeltDock(Actor self, ConveyorBeltDockInfo info)
		: base(self, info)
	{
	}

	Activity IConveyorBeltDockHost.GetInnerDockActivity(Actor self, Actor clientActor, Action continuationCallback, ConveyorBeltInnerDockContext context)
	{
		return new ResourceCrateMovementActivity(clientActor, context.IsLoading, context.Animation, continuationCallback);
	}
}
