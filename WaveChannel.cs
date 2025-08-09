namespace Brackethouse.GB
{
	/// <summary>
	/// Audio channel 3. Plays samples.
	/// </summary>
	class WaveChannel : AudioChannel
	{
		readonly IORegisters IO;
		readonly ushort StartAddress;

		int PeriodDivider = 0;
		int TickCounter = 0;
		const int TicksPerPeriod = 2;

		int LengthTimer = 0;
		bool LengthEnable = false;
		const int LengthLimit = 256;
		int Volume;

		byte DIVAPU = 0;

		int WavePosition = 1;
		const int WaveSize = 32;
		int DACAddress => StartAddress + 0;
		int LengthAddress => StartAddress + 1;
		int OutLevelAddress => StartAddress + 2;
		int PeriodAddress => StartAddress + 3;
		int ControlAddress => StartAddress + 4;
		public WaveChannel(IORegisters io, ushort startAddr)
		{
			IO = io;
			StartAddress = startAddr;
		}
		public override void Step(ushort ticks)
		{
			bool dacOnOff = (IO[DACAddress] & Bit7Mask) != 0;
			byte initialLength = IO[LengthAddress];
			byte outLevel = (byte)(IO[OutLevelAddress] >> 5);
			int period = IO[PeriodAddress] + ((IO[ControlAddress] & Bit012Mask) << 8);
			bool trigger = (IO[ControlAddress] & Bit7Mask) != 0 && IO.WrittenAddress == ControlAddress;
			bool lengthEnable = (IO[ControlAddress] & Bit6Mask) != 0;
			// Turn off if these bits are zero.
			DACPower = dacOnOff;
			if (trigger)
			{
				ChannelEnable = true;
				if (LengthTimer >= LengthLimit)
				{
					LengthTimer = initialLength;
				}
				PeriodDivider = period;
				Volume = outLevel;
			}
			ChannelEnable &= DACPower;
			if (!ChannelEnable)
			{
				WaveValue = 0;
				return;
			}
			LengthEnable = lengthEnable;
			// https://gbdev.io/pandocs/Audio_details.html#div-apu
			DIVAPU += IO.AudioTimerTick;
			if ((DIVAPU % 2) == 0 && LengthEnable && IO.AudioTimerTick != 0)
			{
				// Sound length
				LengthTimer++;
				ChannelEnable &= LengthTimer < LengthLimit;
			}

			TickCounter += ticks;
			while (TickCounter >= TicksPerPeriod)
			{
				TickCounter -= TicksPerPeriod;

				PeriodDivider++;
				const int _11bitOverflow = 0x800;
				if (PeriodDivider >= _11bitOverflow)
				{
					PeriodDivider = period;
					WavePosition++;
					WavePosition %= WaveSize;
					//byte posVol = (byte)(Volume >> 1);
					//byte negVol = (byte)-(Volume >> 1);
					byte value = ReadSample(WavePosition);
					value <<= 0;
					int volShift = Volume - 1;
					if (volShift == -1)
					{
						volShift = 4;
					}
					value >>= volShift;
					WaveValue = value;
				}
			}
		}
		byte ReadSample(int index)
		{
			byte retval = IO[0xff30 + index / 2];
			if (index % 2 == 0)
			{
				retval >>= 4;
			}
			retval &= 0x0f;
			return retval;
		}
	}
}
