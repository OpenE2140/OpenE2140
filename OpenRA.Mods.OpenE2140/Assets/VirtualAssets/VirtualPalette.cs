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

using OpenRA.Primitives;

namespace OpenRA.Mods.OpenE2140.Assets.VirtualAssets;

public class VirtualPalette
{
	public enum Mode
	{
		Merge,
		Replace
	}

	public enum Effect
	{
		Normal,
		Multiply
	}

	public readonly (int Index, Color Color)[][] Colors;
	public readonly Mode Application;
	public readonly Effect ColorEffect;

	private VirtualPalette((int Index, Color Color)[][] colors, Mode application, Effect colorEffect)
	{
		this.Colors = colors;
		this.Application = application;
		this.ColorEffect = colorEffect;
	}

	public static Dictionary<string, VirtualPalette> BuildPaletteEffects(MiniYaml palettes)
	{
		var effects = new Dictionary<string, VirtualPalette>();

		foreach (var paletteNode in palettes.Nodes)
		{
			var name = paletteNode.Key;

			var settings = paletteNode.Value.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			var application = Enum.Parse<Mode>(settings[0]);
			var colorEffect = settings.Length <= 1 ? Effect.Normal : Enum.Parse<Effect>(settings[1]);

			var frameNodes = paletteNode.Value.Nodes.ToList();

			if (frameNodes[0].Value.Nodes.Length == 0)
				frameNodes = new List<MiniYamlNode> { paletteNode };

			var colors = frameNodes.Select(
					frameNode => frameNode.Value.Nodes.SelectMany(
							sequenceNode =>
							{
								var value = sequenceNode.Value.Value.Split(" ", StringSplitOptions.RemoveEmptyEntries);

								if (value.Length is not 3 and not 4)
									throw new Exception("Broken format!");

								var r = int.Parse(value[0]);
								var g = int.Parse(value[1]);
								var b = int.Parse(value[2]);
								var a = value.Length < 4 ? 0xff : int.Parse(value[3]);

								var color = Color.FromArgb(a, r, g, b);

								return SequenceParser.Parse(sequenceNode.Key).Select(index => (index, color));
							}
						)
						.ToArray()
				)
				.ToArray();

			effects.Add(name, new VirtualPalette(colors, application, colorEffect));
		}

		return effects;
	}
}
