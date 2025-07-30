using SDL3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brackethouse.GB
{
	internal class AudioChannel
	{
		const int DIVAPUMask = 0x10;
		byte PrevDIVBit = 0;
		byte DIVAPU = 0;
		IORegisters IO;
		ushort PreviousCPUTick = 0;
		ushort StartAddress;
		nint OutputStream;
		/// <summary>
		/// The four different pulse wave duty cycles.
		/// </summary>
		byte[][] PulseWaves = [
			[000, 255, 255, 255, 255, 255, 255, 255],
			[000, 000, 255, 255, 255, 255, 255, 255],
			[000, 000, 000, 000, 255, 255, 255, 255],
			[000, 000, 000, 000, 000, 000, 255, 255]
			];
		int WavePosition = 0;
		bool Ch1On = false;
		byte Timer = 0;
		bool TimerEnable = false;
		byte Volume = 255;
		int SweepPace = 0;
		int SweepCounter = 0;
		int SweepDirection = 1;

		const int CPUClock = 0x40_0000;
		const double StupidNumber = CPUClock / (double)APU.OutputFrequency;
		double TimeAvailable = 0;
		byte[] OutputBuffer = new byte[APU.OutputFrequency / 30];
		float BufferCursor = 0;
		public AudioChannel(IORegisters io, ushort startAddr)
		{
			IO = io;
			StartAddress = startAddr;
			var spec = new SDL.AudioSpec()
			{
				Channels = 1,
				Format = SDL.AudioFormat.AudioU8,
				Freq = APU.OutputFrequency
			};
			nint stream = SDL.OpenAudioDeviceStream(SDL.AudioDeviceDefaultPlayback, spec, null, 0);
			OutputStream = stream;
			SDL.ResumeAudioStreamDevice(stream);
		}
		public void Step(ushort tick)
		{
			int ticks = tick - PreviousCPUTick;
			if (ticks < 0)
			{
				ticks += ushort.MaxValue + 1;
			}
			PreviousCPUTick = tick;
			TimeAvailable += ticks;

			// https://gbdev.io/pandocs/Audio_details.html#div-apu
			byte div = IO[0xff04];
			div &= DIVAPUMask;
			if (PrevDIVBit != 0 && div == 0)
			{
				DIVAPU++;
				if ((DIVAPU % 8) == 0)
				{
					// Envelope sweep
					if (SweepPace != 0)
					{
						SweepCounter++;
						if (SweepCounter >= SweepPace)
						{
							SweepCounter = 0;
							Volume = (byte)(Volume + SweepDirection);
						}
					}
				}
				if ((DIVAPU % 4) == 0)
				{
					// CH1 freq sweep
				}
				if ((DIVAPU % 2) == 0)
				{
					// Sound length

					if (Ch1On && TimerEnable)
					{
						Timer++;
						if (Timer >= 64)
						{
							Ch1On = false;
						}
					}
				}
			}
			PrevDIVBit = div;
			int ch1WaveDuty = (IO[StartAddress + 1] & 0xc0) >> 6;
			int ch1InitialLength = IO[StartAddress + 1] & 0x3f;

			byte ch1InitialVolume = (byte)(IO[StartAddress + 2] & 0xf0);
			int ch1SweepDir = (byte)(IO[StartAddress + 2] & 0x08) == 0 ? -0x10 : 0x10;
			byte ch1SweepPace = (byte)(IO[StartAddress + 2] & 0x07);

			SweepDirection = ch1SweepDir;
			SweepPace = ch1SweepPace;

			int ch1Period = IO[StartAddress + 3] + ((IO[StartAddress + 4] & 0x03) << 8);
			bool ch1Trigger = (IO[StartAddress + 4] & 0x80) != 0;
			bool ch1LengthEnable = (IO[StartAddress + 4] & 0x40) != 0;
			if (ch1Trigger)
			{
				Ch1On = true;
				TimerEnable = ch1LengthEnable;
				if (Timer >= 64)
				{
					Timer = (byte)ch1InitialLength;
				}
			}


			float ch1Freq = 1048576 / (float)(2048 - ch1Period);
			float freqRatio = (float)APU.OutputFrequency / ch1Freq;
			if (Ch1On && TimeAvailable >= StupidNumber)
			{
				BufferOutput(ch1WaveDuty, freqRatio);
				WavePosition++;
				WavePosition %= PulseWaves[0].Length;
				Volume = ch1InitialVolume;
			}
		}
		void BufferOutput(int wave, float ratio)
		{
			int cursor = (int)Math.Floor(BufferCursor);
			BufferCursor += ratio;
			int cursorEnd = (int)Math.Floor(BufferCursor);
			for (int i = cursor; i < cursorEnd; i++)
			{
				OutputBuffer[i % OutputBuffer.Length] = PulseWaves[wave][WavePosition] == 0 ? (byte)0x00 : Volume;
				TimeAvailable -= StupidNumber;
			}
		}
		/// <summary>
		/// Call once every video frame.
		/// </summary>
		public void FrameStep()
		{
			if (Ch1On)
			{
				//SDL.ClearAudioStream(OutputStream);
				SDL.PutAudioStreamData(OutputStream, OutputBuffer, (int)Math.Floor(BufferCursor));
			}
			BufferCursor = 0;
		}
	}
}
