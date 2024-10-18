using System.Text;
using System.Text.RegularExpressions;

namespace OpenRA.Mods.OpenE2140.Assets.FileFormats;

public class Ini
{
	public readonly string Briefing;
	public readonly string Objective;

	public Ini(Stream stream)
	{
		var text = Encoding.ASCII.GetString(stream.ReadAllBytes()).Trim().Trim('#').Split('#');

		if (text.Length != 4)
			throw new Exception();

		this.Briefing = Regex.Replace(text[2].Trim().Replace("\r\n", "\n").Replace("\r", "\n"), @"(?<=[^\n])\n(?=[^\n])", " ");
		this.Objective = text[3].Trim().Replace("\r\n", "\n").Replace("\r", "\n");
	}
}
