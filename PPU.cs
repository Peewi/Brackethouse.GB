using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brackethouse.GB
{
	/// <summary>
	/// Picture Processing Unit
	/// </summary>
	class PPU
	{
		enum Modes
		{
			HBlank,
			VBlank,
			OAMScan,
			DrawingPixels,
		}
		Memory Memory;
		Modes Mode = Modes.OAMScan;
		byte PixelX;
		byte PixelY;
		const byte Width = 160;
		const byte Height = 144;
		const byte ScanLines = 154;
		const int TicksPerLine = 456;
		const ushort LYAddress = 0xff44;
		int LineTicks = 0;
		ushort PreviousCPUTick = 0;
		public int Frame { get; private set; } = 0;
		public PPU(Memory mem)
		{
			Memory = mem;
		}
		public void Step(ushort tick)
		{
			while (tick != PreviousCPUTick)
			{
				PreviousCPUTick++;
				Dot();
			}
		}
		void Dot()
		{
			// https://gbdev.io/pandocs/Rendering.html

			//TODO: Do actual processing and not just counting.

			LineTicks++;
			if (LineTicks >= TicksPerLine)
			{
				LineTicks = 0;
				PixelX = 0;
				PixelY++;
				Frame += PixelY / ScanLines;
				PixelY %= ScanLines;
				Memory[LYAddress] = PixelY;
				Modes prevMode = Mode;
				Mode = PixelY >= Height ? Modes.VBlank : Modes.OAMScan;
				if (prevMode != Modes.VBlank && Mode == Modes.VBlank)
				{
					FlagVBlankInterrupt();
				}
			}
		}
		void FlagVBlankInterrupt()
		{

		}
	}
}
