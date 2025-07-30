using SDL3;
using System;
using System.Collections.Generic;

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
		const double TicksPerOutputSample = CPUClock / (double)OutputFrequency;
		const int BufferSize = OutputFrequency / 60;
		double TimeAvailable = 0;

		byte[] OutputBuffer = new byte[BufferSize];
		int BufferCursor = 0;
		nint OutputStream;
		AudioChannel[] Channels;
		PulseWaveChannel Channel1;

		public APU(IORegisters io)
		{
			IO = io;
			Channel1 = new PulseWaveChannel(io, ch1Start);
			Channels = [Channel1];

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
			TimeAvailable += ticks;

			const byte bit7Mask = 0x80;
			byte mc = IO[MasterControl];
			bool on = (mc & bit7Mask) == bit7Mask;
			// Reset audio memory if APU is off.
			if (!on)
			{
				for (int i = 0xff10; i < MasterControl; i++)
				{
					IO[i] = 0xff;
				}
				IO[MasterControl] = 0;
				return;
			}
			foreach (AudioChannel chnl in Channels)
			{
				chnl.Step(tick);
			}
			// show on/off status in master control
			byte masterControlValue = 0;
			if (on)
			{
				masterControlValue |= bit7Mask;
			}
			for (int i = 0; i < Channels.Length; i++)
			{
				if (Channels[i].On)
				{
					masterControlValue |= (byte)(0x01 << i);
				}
			}
			IO[MasterControl] = mc;
			// Actual output.
			bool mixTime = TimeAvailable >= TicksPerOutputSample;
			if (!mixTime)
			{
				return;
			}
			TimeAvailable -= TicksPerOutputSample;
			int mixSum = 0;
			int mixCount = 0;
			
			for (int i = 0; i < Channels.Length; i++)
			{
				int leftBit = 0x10 << i;
				int rightBit = 0x01 << i;
				bool leftOn = (IO[Panning] & leftBit) != 0;
				bool rightOn = (IO[Panning] & rightBit) != 0;
				AudioChannel chnl = Channels[i];
				// TODO: proper stereo
				if (chnl.On && (leftOn || rightOn))
				{
					mixCount++;
					mixSum += chnl.WaveValue;
				}
			}
			if (mixCount == 0)
			{
				OutputBuffer[BufferCursor] = 0;
			}
			else
			{
				byte val = (byte)(mixSum / mixCount);
				OutputBuffer[BufferCursor] = val;
			}
			BufferCursor++;
			if (BufferCursor >= BufferSize)
			{
				SDL.PutAudioStreamData(OutputStream, OutputBuffer, BufferCursor);
				BufferCursor = 0;
			}
		}
	}
}
