namespace Brackethouse.GB
{
	internal class PulseWaveChannel : AudioChannel
	{
		byte DIVAPU = 0;
		readonly IORegisters IO;
		readonly ushort StartAddress;
		readonly bool PeriodSweepEnable;
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
		int PeriodSweepPace = 0;
		int PeriodSweepCounter = 0;
		int PeriodSweepDirection = 0;
		int PeriodSweepStep = 0;
		int VolumeSweepPace = 0;
		int VolumeSweepCounter = 0;
		int VolumeSweepDirection = 1;

		int PeriodDivider = 0;
		int TickCounter = 0;
		const int TicksPerPeriod = 4;
		int Ch1SweepAddress => StartAddress + 0;
		int DutyAddress => StartAddress + 1;
		int VolumeAddress => StartAddress + 2;
		int PeriodAddress => StartAddress + 3;
		int ControlAddress => StartAddress + 4;

		public PulseWaveChannel(IORegisters io, ushort startAddr, bool periodSweep)
		{
			IO = io;
			StartAddress = startAddr;
			PeriodSweepEnable = periodSweep;
		}
		public override void Step(ushort ticks)
		{
			int initialLength = IO[DutyAddress] & 0x3f;
			byte initialVolume = (byte)(IO[VolumeAddress] & 0xf0);
			initialVolume >>= 4;

			int period = IO[PeriodAddress] + ((IO[ControlAddress] & Bit012Mask) << 8);
			bool trigger = (IO[ControlAddress] & Bit7Mask) != 0;
			bool lengthEnable = (IO[ControlAddress] & Bit6Mask) != 0;
			// Turn off if these bits are zero.
			DACPower = (IO[VolumeAddress] & Bit34567Mask) != 0;
			if (trigger)
			{
				ChannelEnable = true;
				TimerEnable = lengthEnable;
				PeriodDivider = period;
				Volume = initialVolume;
				if (Timer >= 64)
				{
					Timer = (byte)initialLength;
				}
				if (PeriodSweepEnable)
				{
					PeriodSweepCounter = 0;
					PeriodSweepStep = IO[Ch1SweepAddress] & Bit012Mask;
					PeriodSweepDirection = (IO[Ch1SweepAddress] & 0x08) == 0 ? 1 : -1;
					PeriodSweepPace = (IO[Ch1SweepAddress] >> 4) & Bit012Mask;
				}
				IO[ControlAddress] &= Bit0123456Mask;
			}
			ChannelEnable &= DACPower;
			if (!ChannelEnable)
			{
				WaveValue = 0;
				return;
			}
			if (PeriodSweepEnable)
			{
				int potentialPace = (IO[Ch1SweepAddress] >> 4) & Bit012Mask;
				if (PeriodSweepPace == 0)
				{
					PeriodSweepPace = potentialPace;
				}
				else if (potentialPace == 0)
				{
					PeriodSweepPace = 0;
				}
				PeriodSweepStep = IO[Ch1SweepAddress] & Bit012Mask;
				PeriodSweepDirection = (IO[Ch1SweepAddress] & 0x08) == 0 ? 1 : -1;
			}
			// https://gbdev.io/pandocs/Audio_details.html#div-apu
			if (IO.AudioTimerTick != 0)
			{
				DIVAPU += IO.AudioTimerTick;
				if (VolumeSweepPace != 0 && (DIVAPU % 8) == 0)
				{
					// Envelope sweep
					VolumeSweepCounter++;
					if (VolumeSweepCounter >= VolumeSweepPace)
					{
						VolumeSweepCounter = 0;
						int newVol = Math.Clamp(Volume + VolumeSweepDirection, 0, 15);
						Volume = (byte)newVol;
					}
				}
				if (PeriodSweepPace != 0 && (DIVAPU % 4) == 0)
				{
					// CH1 freq sweep
					PeriodSweepCounter++;
					if (PeriodSweepCounter >= PeriodSweepPace)
					{
						PeriodSweepCounter = 0;
						PeriodSweepPace = (IO[Ch1SweepAddress] >> 4) & Bit012Mask;
						// calculate new period
						int pShadow = period;
						int mod = pShadow >> PeriodSweepStep;
						int newPeriod = pShadow + mod * PeriodSweepDirection;
						if (newPeriod >= 0x800)
						{
							ChannelEnable = false;
							return;
						}
						// Write back new period
						byte pLow = (byte)(newPeriod);
						IO[PeriodAddress] = pLow;
						byte pHigh = (byte)(newPeriod >> 8);
						IO[ControlAddress] &= Bit34567Mask;
						IO[ControlAddress] |= pHigh;
					}
				}
				if (TimerEnable && (DIVAPU % 2) == 0)
				{
					// Sound length
					Timer++;
					ChannelEnable &= Timer < 64;
				}
			}
			//
			TickCounter += ticks;
			int sweepDir = (byte)(IO[VolumeAddress] & 0x08) == 0 ? -1 : 1;
			byte sweepPace = (byte)(IO[VolumeAddress] & 0x07);
			VolumeSweepDirection = sweepDir;
			VolumeSweepPace = sweepPace;
			int ch1WaveDuty = (IO[DutyAddress] & 0xc0) >> 6;
			while (TickCounter >= TicksPerPeriod)
			{
				TickCounter -= TicksPerPeriod;
				PeriodDivider++;
				const int _11bitOverflow = 0x800;
				if (PeriodDivider >= _11bitOverflow)
				{
					PeriodDivider = period;
					WavePosition++;
					WavePosition %= PulseWaves[ch1WaveDuty].Length;
					byte high = Volume;
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
