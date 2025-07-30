using SDL3;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Brackethouse.GB
{
	class APU
	{
		IORegisters IO;
		ushort PreviousCPUTick = 0;
		const ushort ch1Start = 0xff10;
		const ushort ch2Start = 0xff15;
		const ushort ch3Start = 0xff1a;
		const ushort ch4Start = 0xff20;
		const ushort MasterVolume = 0xff24;
		const ushort Panning = 0xff25;
		const ushort MasterControl = 0xff26;
		const int DIVAPUMask = 0x10;

		const int CPUClock = 0x40_0000;
		public const int OutputFrequency = 48_000;
		int TickCounter = 0;
		double TimeAvailable = 0;

		AudioChannel Channel1;

		public APU(IORegisters io)
		{
			IO = io;
			Channel1 = new AudioChannel(io, ch1Start);
		}
		/// <summary>
		/// Call after every CPU instruction
		/// </summary>
		public void Step(ushort tick)
		{
			int ticks = tick - PreviousCPUTick;
			if (ticks < 0)
			{
				ticks += ushort.MaxValue + 1;
			}
			PreviousCPUTick = tick;
			TickCounter += ticks;
			TimeAvailable += ticks;

			const byte bit7Mask = 0x80;
			byte mc = IO[MasterControl];
			bool on = (mc & bit7Mask) == bit7Mask;
			if (!on)
			{
				for (int i = 0xff10; i < MasterControl; i++)
				{
					IO[i] = 0xff;
				}
				IO[MasterControl] = mc;
				return;
			}
			Channel1.Step(tick);
			
		}
		/// <summary>
		/// Call once every video frame.
		/// </summary>
		public void FrameStep()
		{
			Channel1.FrameStep();
		}
	}
}
