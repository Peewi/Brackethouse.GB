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
		protected const byte Bit012Mask = 0x07;
		protected const byte Bit7Mask = 0x80;
		protected const byte Bit0123456Mask = 0x7f;
		protected const byte Bit6Mask = 0x40;
		protected const byte Bit34567Mask = 0xf8;
		/// <summary>
		/// Call during APU Step.
		/// </summary>
		/// <param name="tick">Tick value from CPU</param>
		public abstract void Step(ushort ticks);
	}
}
