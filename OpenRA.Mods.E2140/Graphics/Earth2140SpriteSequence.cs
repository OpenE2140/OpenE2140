using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;

namespace OpenRA.Mods.E2140.Graphics
{
	public class Earth2140SpriteSequenceLoader : DefaultSpriteSequenceLoader
	{
		public Earth2140SpriteSequenceLoader(ModData modData)
			: base(modData)
		{
		}

		public override ISpriteSequence CreateSequence(ModData modData, string tileSet, SpriteCache cache, string sequence, string animation, MiniYaml info)
		{
			return new Earth2140SpriteSequence(modData, tileSet, cache, this, sequence, animation, info);
		}
	}

	public class Earth2140SpriteSequence : DefaultSpriteSequence
	{
		public Earth2140SpriteSequence(ModData modData, string tileSet, SpriteCache cache, ISpriteSequenceLoader loader, string sequence, string animation, MiniYaml info)
			: base(modData, tileSet, cache, loader, sequence, animation, FlipFacings(info))
		{
		}

		private static MiniYaml FlipFacings(MiniYaml info)
		{
			var d = info.ToDictionary();
			if (!LoadField(d, "FlipFacings", false))
				return info;

			var source = info.Value;
			info.Value = null;

			if (d.TryGetValue("Combine", out var templateCombine))
			{
				var combinedFrames = "";

				for (var i = templateCombine.Nodes.Count - 2; i > 0; i--)
				{
					var template = templateCombine.Nodes[i];
					var subFrames = template.Value.Nodes
						.FirstOrDefault(n => n.Key == "Frames").Value.Value;
					var subFramesCount = subFrames.Split(',').Length;

					combinedFrames += $"{source}:\n\tLength: {subFramesCount}\n\tFrames: {subFrames}\n\tAddExtension:false\n\tFlipX:true\n";
				}

				templateCombine.Nodes.AddRange(MiniYaml.FromString(combinedFrames));
			}
			else
			{
				var frames = LoadField<int[]>(d, "Frames", null);

				info.Nodes.Remove(info.Nodes.First(node => node.Key == "Frames"));

				var combinedFrames = "Combine:\n";
				combinedFrames += AppendFlippedFrames(source, frames);

				info.Nodes.Add(MiniYaml.FromString(combinedFrames)[0]);
			}

			return info;
		}

		private static string AppendFlippedFrames(string source, int[] frames)
		{
			var combinedFrames = "";
			for (var i = 0; i < frames.Length; i++)
				combinedFrames += $"\t{source}:\n\t\tStart:{frames[i]}\n\t\tAddExtension:false\n";

			for (var i = frames.Length - 2; i > 0; i--)
				combinedFrames += $"\t{source}:\n\t\tStart:{frames[i]}\n\t\tAddExtension:false\n\t\tFlipX:true\n";

			return combinedFrames;
		}
	}
}
