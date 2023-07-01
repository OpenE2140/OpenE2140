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

using OpenRA.Video;

namespace OpenRA.Mods.OpenE2140.Assets.FileFormats;

public class Flc : IVideo
{
	public ushort FrameCount { get; }
	public byte Framerate { get; }
	public ushort Width { get; }
	public ushort Height { get; }
	public byte[] CurrentFrameData { get; }
	public int CurrentFrameIndex { get; private set; }
	public bool HasAudio => false;
	public byte[] AudioData => Array.Empty<byte>();
	public int AudioChannels => 0;
	public int SampleBits => 0;
	public int SampleRate => 0;

	private readonly byte[][] frames;
	private readonly byte[] palette = new byte[1024];

	public Flc(Stream stream)
	{
		var size = stream.ReadUInt32();

		if (stream.ReadUInt16() != 0xaf12)
			throw new Exception("Broken flc file!");

		this.FrameCount = stream.ReadUInt16();
		this.Width = stream.ReadUInt16();
		this.Height = stream.ReadUInt16();

		this.frames = new byte[this.FrameCount + 1][];
		this.CurrentFrameData = new byte[Exts.NextPowerOf2(this.Height) * Exts.NextPowerOf2(this.Width) * 4];

		var depth = stream.ReadUInt16();

		if (depth != 8)
			throw new Exception("Broken flc file!");

		var flags = stream.ReadUInt16();

		if (flags != 3)
			throw new Exception("Broken flc file!");

		this.Framerate = (byte)(stream.ReadUInt32() / 2);

		if (stream.ReadBytes(2).Any(b => b != 0x00))
			throw new Exception("Broken flc file!");

		stream.ReadUInt32(); // created
		stream.ReadUInt32(); // creator
		stream.ReadUInt32(); // updated
		stream.ReadUInt32(); // updater
		var aspectX = stream.ReadUInt16();
		var aspectY = stream.ReadUInt16();

		if (aspectX > 1 || aspectY > 1)
			throw new Exception("Broken flc file!");

		if (stream.ReadBytes(38).Any(b => b != 0x00))
			throw new Exception("Broken flc file!");

		stream.ReadUInt32(); // frame1
		stream.ReadUInt32(); // frame2

		if (stream.ReadBytes(40).Any(b => b != 0x00))
			throw new Exception("Broken flc file!");

		for (var i = 0; i < this.frames.Length; i++)
			this.frames[i] = stream.ReadBytes((int)(stream.ReadUInt32() - 4));

		if (stream.Position != size)
			throw new Exception("Broken flc file!");

		this.ApplyFrame();
	}

	public void AdvanceFrame()
	{
		this.CurrentFrameIndex++;
		this.ApplyFrame();
	}

	public void Reset()
	{
		this.CurrentFrameIndex = 0;
		this.ApplyFrame();
	}

	private void ApplyFrame()
	{
		var stream = new MemoryStream(this.frames[this.CurrentFrameIndex]);

		if (stream.ReadUInt16() != 0xf1fa)
			throw new Exception("Broken flc file!");

		var chunks = stream.ReadUInt16();
		var delay = stream.ReadUInt16(); // TODO use this somehow

		if (stream.ReadBytes(6).Any(b => b != 0x00))
			throw new Exception("Broken flc frame!");

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
						{
							Array.Copy(stream.ReadBytes(3).Reverse().ToArray(), 0, this.palette, (skipColors + color) * 4, 3);
							this.palette[(skipColors + color) * 4 + 3] = 0xff;
						}
					}

					break;
				}

				case 7:
				{
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
										this.Draw(x++, y, stream.ReadByte());
										this.Draw(x++, y, stream.ReadByte());
									}
								}
								else
								{
									var index1 = stream.ReadByte();
									var index2 = stream.ReadByte();

									for (var j = 0; j < -count; j++)
									{
										this.Draw(x++, y, index1);
										this.Draw(x++, y, index2);
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
									this.Draw(x++, y, index);
							}
							else
							{
								for (var j = 0; j < count; j++)
									this.Draw(x++, y, stream.ReadByte());
							}
						}
					}

					break;
				}

				case 15:
				{
					for (var y = 0; y < this.Height; y++)
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
									this.Draw(x++, y, index);
							}
							else
							{
								for (var j = 0; j < -count; j++)
									this.Draw(x++, y, stream.ReadByte());
							}
						}
					}

					break;
				}

				case 16:
					for (var y = 0; y < this.Height; y++)
					for (var x = 0; x < this.Width; x++)
						this.Draw(x, y, stream.ReadByte());

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

	private void Draw(int x, int y, int index)
	{
		Array.Copy(this.palette, index * 4, this.CurrentFrameData, (y * Exts.NextPowerOf2(this.Width) + x) * 4, 4);
	}
}
