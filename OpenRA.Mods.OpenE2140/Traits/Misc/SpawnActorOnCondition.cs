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

using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Support;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits;

[Desc("Spawns an actor whenever one of the conditions is activated. Works similarly to FreeActor",
	"Works similarly as FreeActor, but allows spawning different actor per each condition",
	"Only one actor is spawned, i.e. AllowRespawn from FreeActor is not supported.")]
public class SpawnActorOnConditionInfo : TraitInfo, IEditorActorOptions
{
	[Desc("Offset relative to the top-left cell of the building.")]
	public readonly CVec SpawnOffset = CVec.Zero;

	[Desc("Which direction the unit should face.")]
	public readonly WAngle Facing = WAngle.Zero;

	[ActorReference(dictionaryReference: LintDictionaryReference.Keys)]
	[Desc("Actors to create, when specified condition is granted. Whichever condition is activated first, wins (i.e. only actor is ever spawned).",
		"A dictionary of [actor name]: [condition].")]
	public readonly Dictionary<string, BooleanExpression> ActorConditions = [];

	[ConsumedConditionReference]
	public IEnumerable<string> LinterSpawnConditions => this.ActorConditions.Values.SelectMany(e => e.Variables).Distinct();

	[Desc("Display order for the free actor checkbox in the map editor")]
	public readonly int EditorFreeActorDisplayOrder = 4;

	IEnumerable<EditorActorOption> IEditorActorOptions.ActorOptions(ActorInfo ai, World world)
	{
		yield return new EditorActorCheckbox("Spawn Child Actor", this.EditorFreeActorDisplayOrder,
			actor =>
			{
				var init = actor.GetInitOrDefault<FreeActorInit>(this);
				if (init != null)
					return init.Value;

				return true;
			},
			(actor, value) => actor.ReplaceInit(new FreeActorInit(this, value), this));
	}

	public override object Create(ActorInitializer init) { return new SpawnActorOnCondition(init, this); }
}

public class SpawnActorOnCondition : IObservesVariables
{
	private readonly SpawnActorOnConditionInfo info;

	protected bool allowSpawn;

	public SpawnActorOnCondition(ActorInitializer init, SpawnActorOnConditionInfo info)
	{
		this.info = info;
		this.allowSpawn = init.GetValue<FreeActorInit, bool>(info, true);
	}

	IEnumerable<VariableObserver> IObservesVariables.GetVariableObservers()
	{
		foreach (var (actor, expression) in this.info.ActorConditions)
		{
			yield return new VariableObserver((self, conditions) => this.RequiredConditionsChanged(self, conditions, actor), expression.Variables);
		}
	}

	private void RequiredConditionsChanged(Actor self, IReadOnlyDictionary<string, int> conditions, string actor)
	{
		if (!this.allowSpawn)
			return;

		var expression = this.info.ActorConditions[actor];
		var shouldSpawn = expression.Evaluate(conditions);

		if (!shouldSpawn)
			return;

		this.allowSpawn = false;

		self.World.AddFrameEndTask(w =>
		{
			w.CreateActor(actor,
			[
				new ParentActorInit(self),
				new LocationInit(self.Location + this.info.SpawnOffset),
				new OwnerInit(self.Owner),
				new FacingInit(this.info.Facing),
			]);
		});
	}
}
