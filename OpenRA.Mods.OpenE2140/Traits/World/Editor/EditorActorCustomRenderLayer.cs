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

using System.Collections.Immutable;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.World.Editor;

public interface ICustomActorEditorRender : ITraitInfoInterface
{
	IEnumerable<IRenderable> RenderAnnotations(EditorActorPreview self, WorldRenderer wr, CustomRenderContext context);
}

public interface ICustomEditorRender : ITraitInfoInterface
{
	IEnumerable<IRenderable> RenderAnnotations(WorldRenderer wr, CustomRenderContext context);
}

public class CustomRenderContext
{
	public IReadOnlyDictionary<string, EditorActorPreview> MapActors { get; }

	public CustomRenderContext(IReadOnlyDictionary<string, EditorActorPreview> mapActors)
	{
		this.MapActors = mapActors;
	}
}

[TraitLocation(SystemActors.EditorWorld)]
[Desc("Required for Water Base annotations in the editor to work. Attach this to the world actor.")]
public class EditorActorCustomRenderLayerInfo : TraitInfo, Requires<EditorActorLayerInfo>
{
	public override object Create(ActorInitializer init) { return new EditorActorCustomRenderLayer(init.Self); }
}

public class EditorActorCustomRenderLayer : ITickRender, IRenderAnnotations, IWorldLoaded
{
	private readonly EditorActorLayer editorLayer;

	private ImmutableDictionary<string, EditorActorPreview> allMapActors = ImmutableDictionary<string, EditorActorPreview>.Empty;
	private ICustomEditorRender[] customRenders = Array.Empty<ICustomEditorRender>();

	public EditorActorCustomRenderLayer(Actor self)
	{
		this.editorLayer = self.Trait<EditorActorLayer>();
	}

	void IWorldLoaded.WorldLoaded(OpenRA.World w, WorldRenderer wr)
	{
		this.customRenders = w.WorldActor.TraitsImplementing<ICustomEditorRender>().ToArray();
	}

	void ITickRender.TickRender(WorldRenderer wr, Actor self)
	{
		var ts = wr.World.Map.Rules.TerrainInfo.TileSize;

		var entireMapBox = new Rectangle(0, 0, wr.World.Map.MapSize.Width * ts.Width, wr.World.Map.MapSize.Height * ts.Height);
		this.allMapActors = this.editorLayer.PreviewsInScreenBox(entireMapBox).ToImmutableDictionary(p => p.ID, StringComparer.InvariantCultureIgnoreCase);
	}

	IEnumerable<IRenderable> IRenderAnnotations.RenderAnnotations(Actor self, WorldRenderer wr)
	{
		var context = new CustomRenderContext(this.allMapActors);

		foreach (var customRender in this.customRenders)
		{
			foreach (var r in customRender.RenderAnnotations(wr, context))
			{
				yield return r;
			}
		}

		foreach (var editorActor in this.allMapActors.Values)
		{
			foreach (var trait in editorActor.Info.TraitInfos<ICustomActorEditorRender>())
			{
				foreach (var r in trait.RenderAnnotations(editorActor, wr, context))
				{
					yield return r;
				}
			}
		}
	}

	bool IRenderAnnotations.SpatiallyPartitionable => false;
}
