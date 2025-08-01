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
		/// <summary>
		/// How many bytes should be in the audio buffer.
		/// </summary>
		/// <remarks>On 60th of a second, one byte for each sound channel.</remarks>
		const int BufferSize = OutputFrequency / 60 * 2;
		double TimeAvailable = 0;
		/// <summary>
		/// Where we hold audio before giving it to SDL.
		/// </summary>
		byte[] OutputBuffer = new byte[BufferSize];
		int BufferCursor = 0;
		nint OutputStream;
		AudioChannel[] Channels;
#if DEBUG
		public int DEBUGNUM { get; private set; }
		List<byte> DifferentOut = [0];
#endif

		public APU(IORegisters io)
		{
			IO = io;
			Channels = [
				new PulseWaveChannel(io, ch1Start),
				new PulseWaveChannel(io, ch2Start),
				new WaveChannel(io, ch3Start),
				new NoiseChannel(io, ch4Start)
				];

			var spec = new SDL.AudioSpec()
			{
				Channels = 2,
				Format = SDL.AudioFormat.AudioU8,
				Freq = OutputFrequency
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
				if (Channels[i].ChannelEnable)
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
			MixAndBuffer();
			if (BufferCursor >= BufferSize)
			{
				SDL.PutAudioStreamData(OutputStream, OutputBuffer, BufferCursor);
				BufferCursor = 0;
#if DEBUG
				DEBUGNUM++;
#endif
			}
		}

		private void MixAndBuffer()
		{
			int leftSum = 0;
			int leftCount = 0;
			int rightSum = 0;
			int rightCount = 0;

			int leftVol = ((IO[MasterVolume] & 0x70) >> 4) + 1;
			int rightVol = (IO[MasterVolume] & 0x07) + 1;

			for (int i = 0; i < Channels.Length; i++)
			{
				int leftBit = 0x10 << i;
				int rightBit = 0x01 << i;
				bool leftOn = (IO[Panning] & leftBit) != 0;
				bool rightOn = (IO[Panning] & rightBit) != 0;
				AudioChannel chnl = Channels[i];
				if (!chnl.DACPower || !chnl.ChannelEnable)
				{
					continue;
				}
				if (leftOn)
				{
					leftCount++;
					leftSum += chnl.WaveValue * leftVol;
				}
				if (rightOn)
				{
					rightCount++;
					rightSum += chnl.WaveValue * rightVol;
				}
			}
			if (leftCount == 0)
			{
				OutputBuffer[BufferCursor] = 0;
			}
			else
			{
				byte val = (byte)(leftSum / leftCount);
				OutputBuffer[BufferCursor] = val;
			}
			if (rightCount == 0)
			{
				OutputBuffer[BufferCursor + 1] = 0;
			}
			else
			{
				byte val = (byte)(rightSum / rightCount);
				OutputBuffer[BufferCursor + 1] = val;
			}
			BufferCursor += 2;
		}
	}
}
