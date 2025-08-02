namespace Brackethouse.GB
{
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
			[0, 0, 0, 0, 0, 0, 0, 1],
			[0, 0, 0, 0, 0, 0, 1, 1],
			[0, 0, 0, 0, 1, 1, 1, 1],
			[0, 0, 1, 1, 1, 1, 1, 1]
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
		int Ch1SweepAddress => StartAddress + 0;
		int DutyAddress => StartAddress + 1;
		int VolumeAddress => StartAddress + 2;
		int PeriodAddress => StartAddress + 3;
		int ControlAddress => StartAddress + 4;

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

			int ch1InitialLength = IO[DutyAddress] & 0x3f;
			byte ch1InitialVolume = (byte)(IO[VolumeAddress] & 0xf0);
			ch1InitialVolume >>= 4;

			int ch1Period = IO[PeriodAddress] + ((IO[ControlAddress] & Bit012Mask) << 8);
			bool ch1Trigger = (IO[ControlAddress] & Bit7Mask) != 0;
			bool ch1LengthEnable = (IO[ControlAddress] & Bit6Mask) != 0;
			// Turn off if these bits are zero.
			DACPower = (IO[VolumeAddress] & Bit34567Mask) != 0;
			if (ch1Trigger)
			{
				ChannelEnable = true;
				TimerEnable = ch1LengthEnable;
				PeriodDivider = ch1Period;
				Volume = ch1InitialVolume;
				if (Timer >= 64)
				{
					Timer = (byte)ch1InitialLength;
				}
				IO[ControlAddress] &= Bit0123456Mask;
			}
			ChannelEnable &= DACPower;
			if (!ChannelEnable)
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
						int newVol = Math.Clamp(Volume + SweepDirection, 0, 15);
						Volume = (byte)newVol;
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
					ChannelEnable &= Timer < 64;
				}
			}
			PrevDIVBit = div;
			//
			TickCounter += ticks;
			int ch1SweepDir = (byte)(IO[StartAddress + 2] & 0x08) == 0 ? -1 : 1;
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
					byte high = Volume;
					//high *= 0x10;
					//high >>= 4;
					byte low = 0;
					WaveValue = PulseWaves[ch1WaveDuty][WavePosition] == 0 ? low : high;
				}
			}
		}
		/// <summary>
		/// Double up the bits in a byte's high nibble, so that it fills the whole byte.
		/// </summary>
		/// <param name="value">A byte value. Only values in the high nibble are used.</param>
		/// <returns>The value expanded to a full byte.</returns>
		byte HighNibbleToByte(byte value)
		{
			byte retval = 0;
			byte bit0 = (byte)(value & 0x10);
			retval += (byte)(bit0 >> 4 + bit0 >> 3);
			byte bit1 = (byte)(value & 0x20);
			retval += (byte)(bit1 >> 3 + bit1 >> 2);
			byte bit2 = (byte)(value & 0x40);
			retval += (byte)(bit2 >> 2 + bit2 >> 1);
			byte bit3 = (byte)(value & 0x80);
			retval += (byte)(bit3 >> 1 + bit3);
			return retval;
		}
	}
}
