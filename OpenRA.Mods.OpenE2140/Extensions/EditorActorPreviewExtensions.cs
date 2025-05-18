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
