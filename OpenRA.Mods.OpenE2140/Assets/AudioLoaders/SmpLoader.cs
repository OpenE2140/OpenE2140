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

using JetBrains.Annotations;

namespace OpenRA.Mods.OpenE2140.Assets.AudioLoaders
{
	[UsedImplicitly]
	public class SmpLoader : ISoundLoader
	{
		private class SmpSoundFormat : ISoundFormat
		{
			public int Channels => 1;
			public int SampleBits => 8;
			public int SampleRate => 16000;
			public float LengthInSeconds => (float)this.data.Length / this.SampleRate;

			private readonly byte[] data;

			public SmpSoundFormat(byte[] data)
			{
				this.data = data;
			}

			public Stream GetPCMInputStream()
			{
				return new MemoryStream(this.data);
			}

			public void Dispose()
			{
				GC.SuppressFinalize(this);
			}
		}

		public bool TryParseSound(Stream stream, out ISoundFormat sound)
		{
			sound = new SmpSoundFormat(stream.ReadAllBytes());

			return true;
		}
	}
}

