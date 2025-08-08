namespace Brackethouse.GB
{
	/// <summary>
	/// Audio channel 4. Makes noise.
	/// </summary>
	class NoiseChannel : AudioChannel
	{
		readonly IORegisters IO;
		readonly ushort StartAddress;
		ushort LFSR = 0;
		bool ShortLFSR = false;
		byte Volume = 0;
		int FrequencyTimer = 0;
		int LengthTimer = 0;

		byte DIVAPU = 0;

		int EnvelopePace = 0;
		int EnvelopeCounter = 0;
		int EnvelopeDirection = 1;

		bool LengthEnable;
		int LengthCounter;

		int LengthAddress => StartAddress + 0;
		int VolumeAddress => StartAddress + 1;
		int FrequencyAddress => StartAddress + 2;
		int ControlAddress => StartAddress + 3;

		public NoiseChannel(IORegisters io, ushort startAddr)
		{
			IO = io;
			StartAddress = startAddr;
		}
		public override void Step(ushort ticks)
		{
			byte initialLength = (byte)(IO[LengthAddress] & 0x3f);

			byte initialVolume = (byte)(IO[VolumeAddress] & 0xf0);
			initialVolume >>= 4;
			int sweepDir = (byte)(IO[VolumeAddress] & 0x08) == 0 ? -1 : 1;
			byte sweepPace = (byte)(IO[VolumeAddress] & Bit012Mask);

			byte clockDivisorCode = (byte)(IO[FrequencyAddress] & Bit012Mask);
			bool lfsrWidth = (IO[FrequencyAddress] & 0x08) != 0;
			byte clockShift = (byte)(IO[FrequencyAddress] & 0xf0);
			clockShift >>= 4;

			int clockDiv = clockDivisorCode * 16;
			if (clockDiv == 0)
			{
				clockDiv = 8;
			}
			clockDiv <<= clockShift;

			bool trigger = (IO[ControlAddress] & Bit7Mask) != 0;
			bool lengthEnable = (IO[ControlAddress] & Bit6Mask) != 0;

			DACPower = (IO[VolumeAddress] & Bit34567Mask) != 0;
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
				IO[ControlAddress] &= Bit0123456Mask;
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
			if (IO.AudioTimerTick != 0)
			{
				DIVAPU += IO.AudioTimerTick;
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
