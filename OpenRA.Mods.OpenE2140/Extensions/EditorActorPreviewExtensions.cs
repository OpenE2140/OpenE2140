using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.OpenE2140.Extensions;

public static class EditorActorPreviewExtensions
{
	public static bool TryGetInit<T>(this EditorActorPreview editorActorPreview, out T init) where T : ActorInit, ISingleInstanceInit
	{
		init = editorActorPreview.GetInitOrDefault<T>();
		return init != null;
	}

	public static bool HasInit<T>(this EditorActorPreview editorActorPreview) where T : ActorInit, ISingleInstanceInit
	{
		return editorActorPreview.GetInitOrDefault<T>() != null;
	}
}
