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
	internal class PulseWaveChannel : AudioChannel
	{
		const int DIVAPUMask = 0x10;
		byte PrevDIVBit = 0;
		byte DIVAPU = 0;
		readonly IORegisters IO;
		ushort PreviousCPUTick = 0;
		readonly ushort StartAddress;
		/// <summary>
		/// The four different pulse wave duty cycles.
		/// </summary>
		readonly byte[][] PulseWaves = [
			[000, 255, 255, 255, 255, 255, 255, 255],
			[000, 000, 255, 255, 255, 255, 255, 255],
			[000, 000, 000, 000, 255, 255, 255, 255],
			[000, 000, 000, 000, 000, 000, 255, 255]
			];
		int WavePosition = 0;
		byte Timer = 0;
		bool TimerEnable = false;
		byte Volume = 255;
		int SweepPace = 0;
		int SweepCounter = 0;
		int SweepDirection = 1;

		int PeriodDivider = 0;
		int TickCounter = 0;
		const int TicksPerPeriod = 4;
		
		public PulseWaveChannel(IORegisters io, ushort startAddr)
		{
			IO = io;
			StartAddress = startAddr;
		}
		public override void Step(ushort tick)
		{
			int ticks = tick - PreviousCPUTick;
			if (ticks < 0)
			{
				ticks += ushort.MaxValue + 1;
			}
			PreviousCPUTick = tick;
			TickCounter += ticks;

			int ch1InitialLength = IO[StartAddress + 1] & 0x3f;
			byte ch1InitialVolume = (byte)(IO[StartAddress + 2] & 0xf0);

			int ch1Period = IO[StartAddress + 3] + ((IO[StartAddress + 4] & 0x03) << 8);
			bool ch1Trigger = (IO[StartAddress + 4] & 0x80) != 0;
			bool ch1LengthEnable = (IO[StartAddress + 4] & 0x40) != 0;
			if (ch1Trigger)
			{
				On = true;
				TimerEnable = ch1LengthEnable;
				PeriodDivider = ch1Period;
				Volume = ch1InitialVolume;
				if (Timer >= 64)
				{
					Timer = (byte)ch1InitialLength;
				}
				IO[StartAddress + 4] &= 0x7f;
			}
			if (!On)
			{
				WaveValue = 0;
				return;
			}
			// https://gbdev.io/pandocs/Audio_details.html#div-apu
			byte div = IO[0xff04];
			div &= DIVAPUMask;
			if (PrevDIVBit != 0 && div == 0)
			{
				DIVAPU++;
				if ((DIVAPU % 8) == 0 && SweepPace != 0)
				{
					// Envelope sweep
					SweepCounter++;
					if (SweepCounter >= SweepPace)
					{
						SweepCounter = 0;
						Volume = (byte)(Volume + SweepDirection);
						if (Volume < 0x10)
						{
							On = false;
						}
					}
				}
				if ((DIVAPU % 4) == 0)
				{
					// CH1 freq sweep
				}
				if ((DIVAPU % 2) == 0 && TimerEnable)
				{
					// Sound length
					Timer++;
					On &= Timer < 64;
				}
			}
			PrevDIVBit = div;
			//
			int ch1SweepDir = (byte)(IO[StartAddress + 2] & 0x08) == 0 ? -0x10 : 0x10;
			byte ch1SweepPace = (byte)(IO[StartAddress + 2] & 0x07);

			SweepDirection = ch1SweepDir;
			SweepPace = ch1SweepPace;
			int ch1WaveDuty = (IO[StartAddress + 1] & 0xc0) >> 6;
			while (TickCounter >= TicksPerPeriod)
			{
				TickCounter -= TicksPerPeriod;
				PeriodDivider++;
				const int _11bitOverflow = 0x800;
				if (PeriodDivider >= _11bitOverflow)
				{
					PeriodDivider = ch1Period;
					WavePosition++;
					WavePosition %= PulseWaves[ch1WaveDuty].Length;
					WaveValue = PulseWaves[ch1WaveDuty][WavePosition] == 0 ? (byte)0x00 : Volume;
				}
			}
		}
	}
}
