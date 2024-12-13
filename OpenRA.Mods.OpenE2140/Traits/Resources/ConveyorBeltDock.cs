using OpenRA.Activities;
using OpenRA.Mods.Common.Traits;
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

	public override bool IsDockingPossible(Actor clientActor, IDockClient client, bool ignoreReservations = false)
	{
		if (!base.IsDockingPossible(clientActor, client, ignoreReservations))
			return false;

		// TODO: this should be temporary until this issue solved in the engine:
		// - if an immovable actor is located on the dock cell, the dock client does not try to find another dock (and is blocked)
		// - this workaround makes the dock client completely ignore this dock host, if there's an immovable actor at the dock cell
		return !clientActor.World.ActorMap
			.GetActorsAt(clientActor.World.Map.CellContaining(this.DockPosition))
			.Any(a => !a.Info.HasTraitInfo<CrateTransporterInfo>() && !a.Info.HasTraitInfo<MobileInfo>());
	}

	Activity IConveyorBeltDockHost.GetInnerDockActivity(Actor self, Actor clientActor, Action continuationCallback, ConveyorBeltInnerDockContext context)
	{
		return new ResourceCrateMovementActivity(clientActor, context.IsLoading, context.Animation, continuationCallback);
	}
}
