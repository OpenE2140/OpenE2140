using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using OpenRA.Mods.Common.Traits.Render;

namespace OpenRA.Mods.OpenE2140.Helpers.Reflection;

public static class RenderSpritesInfoAccessor
{
	[UnsafeAccessor(UnsafeAccessorKind.Field, Name = nameof(RenderSpritesInfo.FactionImages))]
	public static extern ref FrozenDictionary<string, string> FactionImagesField(RenderSpritesInfo @this);
}
