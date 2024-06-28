using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.OpenE2140.Extensions;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.OpenE2140.Traits.Resources;

[TraitLocation(SystemActors.EditorWorld)]
[Desc("Custom support for resource crates in the editor. Attach this to the world actor.")]
public class ResourceCrateEditorLayerInfo : TraitInfo, Requires<EditorActorLayerInfo>
{
	[Desc("The resource crate actor.")]
	public readonly string CrateActor = "crate";

	public override object Create(ActorInitializer init)
	{
		return new ResourceCrateEditorLayer(this);
	}
}

public class ResourceCrateEditorLayer : ITickRender
{
	private readonly ResourceCrateEditorLayerInfo info;

	private readonly Lazy<EditorViewportControllerWidget> editor = Exts.Lazy(() => Ui.Root.Get<EditorViewportControllerWidget>("MAP_EDITOR"));

	private EditorViewportControllerWidget Editor => this.editor.Value;

	public ResourceCrateEditorLayer(ResourceCrateEditorLayerInfo info)
	{
		this.info = info;
	}

	void ITickRender.TickRender(WorldRenderer wr, Actor self)
	{
		if (this.Editor?.CurrentBrush is EditorDefaultBrush editorDefaultBrush)
			TryClearFacing(editorDefaultBrush.Selection.Actor);
		else if (this.Editor?.CurrentBrush is EditorActorBrush editorActorBrush)
			TryClearFacing(editorActorBrush.Preview);

		void TryClearFacing(EditorActorPreview? actor)
		{
			if (actor != null && this.info.CrateActor.Equals(actor.Info.Name, StringComparison.InvariantCultureIgnoreCase) && actor.HasInit<FacingInit>())
				actor.RemoveInits<FacingInit>();
		}
	}
}
