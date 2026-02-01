using OpenRA.FileSystem;
using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Mods.OpenE2140.Widgets.Util;

public class SpriteSheetProvider : IDisposable
{
	private readonly Cache<SheetType, SheetBuilder> sheetBuilders;
	private readonly Cache<string, Sprite[]> spriteCache;

	private bool disposed;

	public SpriteSheetProvider(int sheetSize, IReadOnlyFileSystem fileSystem, ISpriteLoader[] spriteLoaders)
	{
		this.sheetBuilders = new Cache<SheetType, SheetBuilder>(t => new SheetBuilder(t, sheetSize));
		this.spriteCache = new Cache<string, Sprite[]>(
			filename => FrameLoader.GetFrames(fileSystem, filename, spriteLoaders, out _)
				.Select(f => this.sheetBuilders[SheetBuilder.FrameTypeToSheetType(f.Type)].Add(f))
				.ToArray());
	}

	public Sprite[] GetSprites(string filename)
	{
		ObjectDisposedException.ThrowIf(this.disposed, this);

		return this.spriteCache[filename];
	}

	public void Dispose()
	{
		if (this.disposed)
			return;

		this.disposed = true;

		foreach (var builder in this.sheetBuilders.Values)
		{
			builder.Dispose();
		}

		this.sheetBuilders.Clear();
		this.spriteCache.Clear();

		GC.SuppressFinalize(this);
	}
}
