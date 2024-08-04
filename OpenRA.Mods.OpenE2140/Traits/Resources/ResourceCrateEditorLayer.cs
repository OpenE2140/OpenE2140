using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

[TraitLocation(SystemActors.EditorWorld)]
[Desc("Custom support for resource crates in the editor. Attach this to the world actor.")]
public class ResourceCrateEditorLayerInfo : TraitInfo, Requires<EditorActorLayerInfo>
{
	[Desc("The resource crate actor.")]
	public readonly string CrateActor = "crate";

	public override object Create(ActorInitializer init)
	{
		return new ResourceCrateEditorLayer(init.Self, this);
	}
}

public class ResourceCrateEditorLayer : ITickRender
{
	private readonly ResourceCrateEditorLayerInfo info;
	private readonly EditorCursorLayer editorCursorLayer;

	public ResourceCrateEditorLayer(Actor self, ResourceCrateEditorLayerInfo info)
	{
		this.info = info;
		this.editorCursorLayer = self.Trait<EditorCursorLayer>();
	}

	void ITickRender.TickRender(WorldRenderer wr, Actor self)
	{
		if (this.editorCursorLayer.Actor?.Info.Name == this.info.CrateActor &&
			this.editorCursorLayer.Actor.HasInit<FacingInit>())
		{
			this.editorCursorLayer.Actor.RemoveInits<FacingInit>();
		}
	}
}
