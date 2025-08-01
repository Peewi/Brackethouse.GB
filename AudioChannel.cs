using SDL3;
using System;
using System.Collections.Generic;

namespace Brackethouse.GB
{
	internal abstract class AudioChannel
	{
		/// <summary>
		/// Is the channel enabled.
		/// </summary>
		public bool ChannelEnable { get; protected set; }
		/// <summary>
		/// Is the channel's DAC turned on.
		/// </summary>
		public bool DACPower { get; protected set; }
		/// <summary>
		/// Value this sound channel is currently outputting. 0-15.
		/// </summary>
		public byte WaveValue { get; protected set; } = 0;
		/// <summary>
		/// Call during APU Step.
		/// </summary>
		/// <param name="tick">Tick value from CPU</param>
		public abstract void Step(ushort tick);
	}
}
