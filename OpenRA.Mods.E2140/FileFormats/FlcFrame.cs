#region Copyright & License Information

/*
 * Copyright 2007-2022 The Earth 2140 Developers (see AUTHORS)
 * This file is part of Earth 2140, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */

#endregion

using OpenRA.Primitives;

namespace OpenRA.Mods.E2140.FileFormats;

public class FlcFrame
{
	public readonly Color[] Pixels;
	public readonly ushort Delay;

	public FlcFrame(Stream stream, IList<Color> palette, Flc flc, Color[] baseFrame)
	{
		var chunks = stream.ReadUInt16();
		this.Delay = stream.ReadUInt16();

		if (stream.ReadBytes(6).Any(b => b != 0x00))
			throw new Exception("Broken flc frame!");

		this.Pixels = new Color[baseFrame.Length];

		for (var i = 0; i < chunks; i++)
		{
			var chunkStart = stream.Position;
			var chunkSize = stream.ReadUInt32();
			var chunkType = stream.ReadUInt16();

			switch (chunkType)
			{
				case 4:
				{
					var numChunks = stream.ReadUInt16();

					for (var chunk = 0; chunk < numChunks; chunk++)
					{
						var skipColors = stream.ReadByte();
						var numColors = stream.ReadByte();

						if (numColors == 0)
							numColors = 256;

						for (var color = 0; color < numColors; color++)
							palette[skipColors + color] = Color.FromArgb(stream.ReadByte(), stream.ReadByte(), stream.ReadByte());
					}

					break;
				}

				case 7:
				{
					Array.Copy(baseFrame, this.Pixels, this.Pixels.Length);

					var numLines = stream.ReadUInt16();
					var y = 0;

					while (numLines > 0)
					{
						var numChunks = stream.ReadInt16();

						if (numChunks > 0)
						{
							numLines--;
							var x = 0;

							for (var chunk = 0; chunk < numChunks; chunk++)
							{
								x += stream.ReadByte();
								var count = (sbyte)stream.ReadByte();

								if (count > 0)
								{
									for (var j = 0; j < count; j++)
									{
										this.Pixels[y * flc.Width + x++] = palette[stream.ReadByte()];
										this.Pixels[y * flc.Width + x++] = palette[stream.ReadByte()];
									}
								}
								else
								{
									var index1 = stream.ReadByte();
									var index2 = stream.ReadByte();

									for (var j = 0; j < -count; j++)
									{
										this.Pixels[y * flc.Width + x++] = palette[index1];
										this.Pixels[y * flc.Width + x++] = palette[index2];
									}
								}
							}

							y++;
						}
						else
							y += -numChunks;
					}

					break;
				}

				case 12:
				{
					var firstLine = stream.ReadUInt16();
					var numLines = stream.ReadUInt16();

					for (var y = firstLine; y < firstLine + numLines; y++)
					{
						var numChunks = stream.ReadByte();
						var x = 0;

						for (var chunk = 0; chunk < numChunks; chunk++)
						{
							x += stream.ReadByte();
							var count = (sbyte)stream.ReadByte();

							if (count < 0)
							{
								var index = stream.ReadByte();

								for (var j = 0; j < -count; j++)
									this.Pixels[y * flc.Width + x++] = palette[index];
							}
							else
							{
								for (var j = 0; j < count; j++)
									this.Pixels[y * flc.Width + x++] = palette[stream.ReadByte()];
							}
						}
					}

					break;
				}

				case 15:
				{
					for (var y = 0; y < flc.Height; y++)
					{
						var numChunks = stream.ReadByte();
						var x = 0;

						for (var chunk = 0; chunk < numChunks; chunk++)
						{
							var count = (sbyte)stream.ReadByte();

							if (count > 0)
							{
								var index = stream.ReadByte();

								for (var j = 0; j < count; j++)
									this.Pixels[y * flc.Width + x++] = palette[index];
							}
							else
							{
								for (var j = 0; j < -count; j++)
									this.Pixels[y * flc.Width + x++] = palette[stream.ReadByte()];
							}
						}
					}

					break;
				}

				case 16:
					for (var pixel = 0; pixel < this.Pixels.Length; pixel++)
						this.Pixels[pixel] = palette[stream.ReadByte()];

					break;

				case 18:
					// TODO this is a thumbnail image.
					stream.Position += chunkSize - 6;

					break;

				default:
					throw new Exception("Broken flc frame!");
			}

			if (stream.Position - chunkStart != chunkSize)
				stream.Position += chunkStart + chunkSize - stream.Position;
		}
	}
}
