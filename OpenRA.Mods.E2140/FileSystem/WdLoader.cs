using System;
using System.Collections.Generic;
using System.IO;
using OpenRA.FileSystem;
using OpenRA.Primitives;

namespace OpenRA.Mods.E2140.FileSystem
{
	public class WdLoader : IPackageLoader
	{
		private class WdEntry
		{
			public int Offset;
			public int Length;
		}

		public sealed class WdFile : IReadOnlyPackage
		{
			public string Name { get; private set; }
			public IEnumerable<string> Contents { get { return index.Keys; } }

			private readonly Dictionary<string, WdEntry> index = new Dictionary<string, WdEntry>();
			private readonly Stream stream;

			public WdFile(Stream stream, string filename)
			{
				this.stream = stream;
				Name = filename;

				var numFiles = stream.ReadUInt32();

				if (numFiles == 0)
					return; // TODO implement sound container support

				for (var i = 0; i < numFiles; i++)
				{
					var entry = new WdEntry { Offset = stream.ReadInt32(), Length = stream.ReadInt32() };
					/*var unk3 = */
					stream.ReadUInt32(); // 0x00
					/*var unk4 = */
					stream.ReadUInt32(); // 0x00
					/*var unk5 = */
					stream.ReadUInt32(); // 0x00
					var filePathOffset = stream.ReadUInt32();

					var originalPosition = stream.Position;
					stream.Position = numFiles * 24 + 8 + filePathOffset;
					index.Add(stream.ReadASCIIZ(), entry);
					stream.Position = originalPosition;
				}
			}

			public Stream GetStream(string filename)
			{
				WdEntry entry;
				if (!index.TryGetValue(filename, out entry))
					return null;

				return SegmentStream.CreateWithoutOwningStream(stream, entry.Offset, entry.Length);
			}

			public bool Contains(string filename)
			{
				return index.ContainsKey(filename);
			}

			public IReadOnlyPackage OpenPackage(string filename, OpenRA.FileSystem.FileSystem context)
			{
				IReadOnlyPackage package;
				var childStream = GetStream(filename);
				if (childStream == null)
					return null;

				if (context.TryParsePackage(childStream, filename, out package))
					return package;

				childStream.Dispose();
				return null;
			}

			public void Dispose()
			{
				stream.Dispose();
			}
		}

		public bool TryParsePackage(Stream s, string filename, OpenRA.FileSystem.FileSystem context, out IReadOnlyPackage package)
		{
			if (!filename.EndsWith(".wd", StringComparison.InvariantCultureIgnoreCase))
			{
				package = null;
				return false;
			}

			package = new WdFile(s, filename);
			return true;
		}
	}
}
