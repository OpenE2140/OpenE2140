#region Copyright & License Information

/*
 * Copyright (c) The OpenE2140 Developers and Contributors
 * This file is part of OpenE2140, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */

#endregion

using OpenRA.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Traits.Resources.Activities;

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

public class ConveyorBeltDockInfo : SharedDockHostInfo
{
	[Desc("Sequence for crate movement during load animation")]
	[FieldLoader.LoadUsing(nameof(LoadLoadCrateMoveSequence), required: true)]
	public readonly CrateMoveSequence LoadSequence = null!;

	[Desc("Sequence for crate movement during unload animation")]
	[FieldLoader.LoadUsing(nameof(LoadUnloadCrateMoveSequence), required: true)]
	public readonly CrateMoveSequence UnloadSequence = null!;

	private static object LoadLoadCrateMoveSequence(MiniYaml parentNode)
	{
		return CrateMoveSequence.Load(parentNode, nameof(LoadSequence));
	}

	private static object LoadUnloadCrateMoveSequence(MiniYaml parentNode)
	{
		return CrateMoveSequence.Load(parentNode, nameof(UnloadSequence));
	}

	public override object Create(ActorInitializer init)
	{
		return new ConveyorBeltDock(init.Self, this);
	}
}

public class CrateMoveSequence
{
	public readonly int[] Delays = [];
	public readonly int[] Offsets = [];

	public CrateMoveSequence()
	{
	}

	public CrateMoveSequence(int[] delays, int[] offsets)
	{
		this.Delays = delays;
		this.Offsets = offsets;
	}

	public static CrateMoveSequence Load(MiniYaml parentNode, string key)
	{
		var node = parentNode.NodeWithKeyOrDefault(key) ?? throw new YamlException($"{key} not defined");

		return FieldLoader.Load<CrateMoveSequence>(node.Value);
	}
}

public class ConveyorBeltDock : SharedDockHost, IConveyorBeltDockHost
{
	private readonly CrateMoveSequence loadSequence;
	private readonly CrateMoveSequence unloadSequence;

	public new readonly ConveyorBeltDockInfo Info;

	public ConveyorBeltDock(Actor self, ConveyorBeltDockInfo info)
		: base(self, info)
	{
		this.Info = info;
		this.loadSequence = info.LoadSequence;
		this.unloadSequence = info.UnloadSequence;
	}

	Activity IConveyorBeltDockHost.GetInnerDockActivity(Actor self, Actor clientActor, Action continuationCallback, ConveyorBeltInnerDockContext context)
	{
		return new ResourceCrateMovementActivity(clientActor, context.IsLoading, context.Animation,
			crateMoveSequence: context.IsLoading ? this.loadSequence : this.unloadSequence,
			continuationCallback);
	}
}
