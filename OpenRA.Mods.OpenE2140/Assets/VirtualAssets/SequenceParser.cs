using System.Text.RegularExpressions;

namespace OpenRA.Mods.OpenE2140.Assets.VirtualAssets
{
	public static class SequenceParser
	{
		public static IEnumerable<int> Parse(string sequenceString)
		{
			var sequence = new List<int>();

			foreach (var segment in sequenceString.Split(',', StringSplitOptions.RemoveEmptyEntries))
			{
				var match = Regex.Match(segment, "^(\\d+)(?:-(\\d+)(?:\\[(\\d+)_(\\d+)_(\\d+)\\])?)?$");

				if (!match.Success)
					throw new Exception("Broken format!");

				var firstFrame = int.Parse(match.Groups[1].Value);
				var lastFrame = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : firstFrame;
				var skipBefore = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
				var take = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : 1;
				var skipAfter = match.Groups[5].Success ? int.Parse(match.Groups[5].Value) : 0;

				var numFrames = Math.Abs(firstFrame - lastFrame) + 1;
				var direction = firstFrame < lastFrame ? 1 : -1;
				var segmentLength = skipBefore + take + skipAfter;
				var trailing = segmentLength - skipAfter;

				for (var i = 0; i < numFrames; i++)
				{
					var frame = firstFrame + i * direction;
					var segmentIndex = i % segmentLength;

					if (segmentIndex >= skipBefore && segmentIndex < trailing)
						sequence.Add(frame);
				}
			}

			return sequence;
		}
	}
}

