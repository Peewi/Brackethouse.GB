namespace Brackethouse.GB
{
	/// <summary>
	/// Audio channel 3. Plays samples.
	/// </summary>
	class WaveChannel : AudioChannel
	{
		readonly IORegisters IO;
		readonly ushort StartAddress;
		ushort PreviousCPUTick = 0;

		int PeriodDivider = 0;
		int TickCounter = 0;
		const int TicksPerPeriod = 2;

		byte LengthTimer = 0;
		bool LengthEnable = false;
		const byte LengthLimit = 64;
		int Volume;

		const int DIVAPUMask = 0x10;
		byte PrevDIVBit = 0;
		byte DIVAPU = 0;

		int WavePosition = 1;
		const int WaveSize = 32;
		public WaveChannel(IORegisters io, ushort startAddr)
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

			bool dacOnOff = (IO[StartAddress] & 0x80) != 0;
			byte initialLength = IO[StartAddress + 1];
			byte outLevel = (byte)(IO[StartAddress + 2] >> 5);
			int period = IO[StartAddress + 3] + ((IO[StartAddress + 4] & 0x03) << 8);
			bool trigger = (IO[StartAddress + 4] & 0x80) != 0;
			bool lengthEnable = (IO[StartAddress + 4] & 0x40) != 0;
			if (trigger)
			{
				On = true;
				if (LengthTimer >= LengthLimit)
				{
					LengthTimer = initialLength;
				}
				PeriodDivider = period;
				Volume = outLevel;
				IO[StartAddress + 4] &= 0x7f;
			}
			On &= dacOnOff;
			if (!On)
			{
				return;
			}
			LengthEnable = lengthEnable;
			// https://gbdev.io/pandocs/Audio_details.html#div-apu
			byte div = IO[0xff04];
			div &= DIVAPUMask;
			if (PrevDIVBit != 0 && div == 0)
			{
				DIVAPU++;
				if ((DIVAPU % 2) == 0 && LengthEnable)
				{
					// Sound length
					LengthTimer++;
					On &= LengthTimer < LengthLimit;
				}
			}
			PrevDIVBit = div;

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
					switch (Volume)
					{
						case 0:
							value = 0;
							break;
						case 1:
							break;
						case 2:
							value >>= 1;
							break;
						case 3:
							value >>= 2;
							break;
						default:
							break;
					}
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
