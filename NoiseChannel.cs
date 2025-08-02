namespace Brackethouse.GB
{
	/// <summary>
	/// Audio channel 4. Makes noise.
	/// </summary>
	class NoiseChannel : AudioChannel
	{
		readonly IORegisters IO;
		readonly ushort StartAddress;
		ushort PreviousCPUTick = 0;
		ushort LFSR = 0;
		bool ShortLFSR = false;
		byte Volume = 0;
		int FrequencyTimer = 0;
		int LengthTimer = 0;

		byte PrevDIVBit = 0;
		byte DIVAPU = 0;

		int EnvelopePace = 0;
		int EnvelopeCounter = 0;
		int EnvelopeDirection = 1;

		bool LengthEnable;
		int LengthCounter;

		public NoiseChannel(IORegisters io, ushort startAddr)
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

			byte initialLength = (byte)(IO[StartAddress + 0] & 0x3f);

			byte initialVolume = (byte)(IO[StartAddress + 1] & 0xf0);
			initialVolume >>= 4;
			int sweepDir = (byte)(IO[StartAddress + 1] & 0x08) == 0 ? -1 : 1;
			byte sweepPace = (byte)(IO[StartAddress + 1] & 0x07);

			byte clockDivisorCode = (byte)(IO[StartAddress + 2] & 0x07);
			bool lfsrWidth = (IO[StartAddress + 2] & 0x08) != 0;
			byte clockShift = (byte)(IO[StartAddress + 2] & 0xf0);
			clockShift >>= 4;

			int clockDiv = clockDivisorCode * 16;
			if (clockDiv == 0)
			{
				clockDiv = 8;
			}
			clockDiv <<= clockShift;

			bool trigger = (IO[StartAddress + 3] & 0x80) != 0;
			bool lengthEnable = (IO[StartAddress + 3] & 0x40) != 0;

			DACPower = (IO[StartAddress + 1] & 0xf8) != 0;
			if (trigger)
			{
				ChannelEnable = true;
				Volume = initialVolume;
				LFSR = 0;
				FrequencyTimer = clockDiv;
				if (LengthTimer <= 0)
				{
					LengthTimer = 64 - initialLength;
				}
				LengthEnable = lengthEnable;
				IO[StartAddress + 3] &= 0x7f;
			}
			ShortLFSR = lfsrWidth;
			EnvelopePace = sweepPace;
			EnvelopeDirection = sweepDir;

			ChannelEnable &= DACPower;
			if (!ChannelEnable)
			{
				WaveValue = 0;
				return;
			}
			for (int i = 0; i < ticks; i++)
			{
				FrequencyTimer--;
				if (FrequencyTimer == 0)
				{
					Shift();
					FrequencyTimer = clockDiv;
				}
			}
			// https://gbdev.io/pandocs/Audio_details.html#div-apu
			byte div = IO[0xff04];
			const int DIVAPUMask = 0x10;
			div &= DIVAPUMask;
			if (PrevDIVBit != 0 && div == 0)
			{
				DIVAPU++;
				if ((DIVAPU % 8) == 0 && EnvelopePace != 0)
				{
					// Envelope sweep
					EnvelopeCounter--;
					if (EnvelopeCounter <= 0)
					{
						EnvelopeCounter = EnvelopePace;
						int newVol = Math.Clamp(Volume + EnvelopeDirection, 0, 15);
						Volume = (byte)newVol;
					}
				}
				if ((DIVAPU % 2) == 0 && LengthEnable)
				{
					// Sound length
					LengthCounter--;
					ChannelEnable &= LengthCounter > 0;
				}
			}
			PrevDIVBit = div;
		}

		void Shift()
		{
			// https://gbdev.io/pandocs/Audio_details.html#noise-channel-ch4
			const int registerSize = 15;
			const int shortRegisterSize = 7;
			ushort bit0 = (ushort)(LFSR & 0x01);
			ushort bit1 = (ushort)(LFSR & 0x02);
			bit1 >>= 1;
			ushort xnor = (ushort)(bit0 == bit1 ? 1 : 0);
			xnor <<= registerSize;
			LFSR |= xnor;
			if (ShortLFSR)
			{
				xnor >>= registerSize;
				xnor <<= shortRegisterSize;
				const ushort mask = 0xff7f;
				LFSR &= mask;
				LFSR |= xnor;
			}
			ushort finalBit = (ushort)(LFSR & 0x01);
			byte high = Volume;
			byte low = 0;
			WaveValue = finalBit == 0 ? low : high;
			LFSR >>= 1;
		}
	}
}
