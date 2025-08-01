using SDL3;
using System;
using System.Collections.Generic;

namespace Brackethouse.GB
{
	internal abstract class AudioChannel
	{
		public bool On { get; protected set; }
		public byte WaveValue { get; protected set; } = 0;
		public abstract void Step(ushort tick);
	}
}
