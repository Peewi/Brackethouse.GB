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
			HBlank = 0,
			VBlank = 1,
			OAMScan = 2,
			DrawingPixels = 3,
		}
		const ushort VideoRAMStart = 0x8000;
		const ushort VideoRAMEnd = 0x9FFF;
		const ushort ObjectAttributeMemoryStart = 0xFE00;
		const ushort ObjectAttributeMemoryEnd = 0xFE9F;
		readonly IORegisters IO;
		Modes Mode = Modes.OAMScan;
		byte PixelX;
		byte PixelY;
		const byte Width = 160;
		const byte Height = 144;
		const byte ScanLines = 154;
		const int TicksPerLine = 456;
		public const int LCDCAddress = 0xff40;
		public const int LCDStatusAddress = 0xff41;
		public const ushort LYAddress = 0xff44;
		public const ushort LYCompareAddress = 0xff45;
		int LineTicks = 0;
		ushort PreviousCPUTick = 0;
		byte[] VRAM = new byte[0x2000];
		byte[] OAM = new byte[0xa0];
		public int Frame { get; private set; } = 0;

		byte LineObjectCount = 0;
		const int MaxObjectsPerLine = 10;
		byte[] LineObjects = new byte[MaxObjectsPerLine];

		Display Display;

		public PPU(IORegisters io, Display dsp)
		{
			IO = io;
			Display = dsp;
		}
		public void Step(ushort tick)
		{
			while (tick != PreviousCPUTick)
			{
				PreviousCPUTick++;
				Dot();
			}
			UpdateLCDStatus();
			IO[LYAddress] = PixelY;
		}
		void Dot()
		{
			// https://gbdev.io/pandocs/Rendering.html

			Modes prevMode = Mode;
			//TODO: Do actual processing and not just counting.
			if (PixelY < Height)
			{
				if (LineTicks == 0)
				{
					// I'm just gonna do the OAM scan in one go.
					OAMScan();
					Mode = Modes.OAMScan;

				}
				else if (LineTicks == 80)
				{
					Mode = Modes.DrawingPixels;
				}
			}
			if (Mode == Modes.DrawingPixels)
			{
				DrawPixel(PixelX, PixelY);
				PixelX++;
				if (PixelX >= Width)
				{
					Mode = Modes.HBlank;
				}
			}
			LineTicks++;
			if (LineTicks >= TicksPerLine)
			{
				LineTicks = 0;
				PixelX = 0;
				PixelY++;
				Frame += PixelY / ScanLines;
				PixelY %= ScanLines;
				Mode = PixelY >= Height ? Modes.VBlank : Mode;
				if (prevMode != Modes.VBlank && Mode == Modes.VBlank)
				{
					FlagVBlankInterrupt();
				}
			}
		}

		private void UpdateLCDStatus()
		{
			byte status = IO[LCDStatusAddress];
			status &= 0b1111_1000;
			byte LYCBit = (byte)(PixelY == IO[LYCompareAddress] ? 0b0100 : 0);
			byte modeBits = (byte)((byte)Mode & 0b0011);
			status |= LYCBit;
			status |= modeBits;
			IO[LCDStatusAddress] = status;
		}

		void DrawPixel(byte x, byte y)
		{
			byte lcdc = IO[LCDCAddress];
			bool bgEnable = (0b0000_0001 & lcdc) != 0;
			bool objectEnable = (0b0000_0010 & lcdc) != 0;
			bool objectSize = (0b0000_0100 & lcdc) != 0;
			bool bgTileMap = (0b0000_1000 & lcdc) != 0;
			bool bgTileSrc = (0b0001_0000 & lcdc) != 0;
			bool windowEnable = (0b0010_0000 & lcdc) != 0;
			bool windowTileMap = (0b0100_0000 & lcdc) != 0;
			bool LCDEnable = (0b1000_0000 & lcdc) != 0;

			if (!LCDEnable)
			{
				//return;
			}

			byte sy = IO[0xff42];
			byte sx = IO[0xff43];
			byte wy = IO[0xff4a];
			byte wx = IO[0xff4b];
			byte tilemapPosX = x;
			tilemapPosX += sx;
			byte tilemapPosY = y;
			tilemapPosY += sy;

			byte tilesizePx = 8;
			byte tileX = tilemapPosX;
			tileX /= tilesizePx;
			byte tileY = tilemapPosY;
			tileY /= tilesizePx;

			const byte mapWidth = 32;
			ushort tileNumber = (ushort)(tileY * mapWidth + tileX);
			byte tilePX = tilemapPosX;
			tilePX %= tilesizePx;
			byte tilePY = tilemapPosY;
			tilePY %= tilesizePx;

			byte pxVal = ReadTilePixel(bgTileMap, bgTileSrc, tileNumber, tilePX, tilePY);
			Display.SetPixel(PixelX, PixelY, pxVal);
		}
		byte ReadTilePixel(bool Altmap, bool altTiles, ushort mapTileIndex, byte x, byte y)
		{
			const ushort map1Addr = 0x9800;
			const ushort map2Addr = 0x9C00;
			ushort tAddr = map1Addr;
			if (Altmap)
			{
				tAddr = map2Addr;
			}
			tAddr += mapTileIndex;
			byte tileIndex = SelfReadVRAM(tAddr);
			ushort tileStart = (ushort)(0x8000 + tileIndex * 16);
			if (altTiles)
			{
				tileStart = (ushort)(0x9000 + (sbyte)tileIndex * 16);
			}
			x = (byte)(7 - x);
			byte bitMask = (byte)(1 << x);
			ushort byte1 = (ushort)(tileStart + y * 2);
			ushort byte2 = (ushort)(byte1 + 1);
			byte data = (byte)((SelfReadVRAM(byte1) & bitMask) >> x);
			data |= (byte)(((SelfReadVRAM(byte2) & bitMask) >> x) << 1);
			return data;
		}
		/// <summary>
		/// Do the OAM scan, but in all one go instead of over 80 ticks like on real hardware.
		/// </summary>
		void OAMScan()
		{
			const int oamSize = 40;
			LineObjectCount = 0;
			const int objByteSize = 4;
			const int objHeight = 8;
			for (int i = 0; i < oamSize; i++)
			{
				int yAddr = ObjectAttributeMemoryStart + objByteSize * i;
				byte y = SelfReadOAM(yAddr);
				if (PixelY >= y && PixelY < y + objHeight)
				{
					// if object found
					LineObjects[LineObjectCount] = (byte)i;
					LineObjectCount++;
					if (LineObjectCount == MaxObjectsPerLine)
					{
						break;
					}
				}
			}
		}
		void FlagVBlankInterrupt()
		{
			const ushort interruptFlag = 0xff0f;
			const int vBlankBit = 0b0000_0001;
			IO[interruptFlag] |= vBlankBit;
		}
		public void DumpTiles()
		{
			int tileSize = 8;
			for (int y = 0; y < 128; y++)
			{
				for (int x = 0; x < 128; x++)
				{
					int tileX = x / tileSize;
					int tileY = y / tileSize;
					int tilePX = x % tileSize;
					int tilePY = y % tileSize;

					ushort tileStart = (ushort)(0x8000 + tileY * 16 * 16 + tileX * 16);
					tilePX = 7 - tilePX;
					byte bitMask = (byte)(1 << tilePX);
					ushort byte1 = (ushort)(tileStart + tilePY * 2);
					ushort byte2 = (ushort)(byte1 + 1);
					byte data = (byte)((SelfReadVRAM(byte1) & bitMask) >> tilePX);
					data |= (byte)(((SelfReadVRAM(byte2) & bitMask) >> tilePX) << 1);

					Display.SetPixel((byte)x, (byte)y, data);
				}
			}
			Display.Output();
		}
		/// <summary>
		/// The CPU would like to read VRAM.
		/// </summary>
		/// <param name="address">Memory address to read from</param>
		/// <returns>Value at given address, or 0xff if the PPU is currently drawing pixels</returns>
		public byte CPUReadVRAM(int address)
		{
			if (Mode == Modes.DrawingPixels)
			{
				return 0xff;
			}
			return VRAM[address - VideoRAMStart];
		}
		/// <summary>
		/// Read from VRAM, using a memory address
		/// </summary>
		/// <param name="address">Memory address to read from</param>
		/// <returns>Value at given address</returns>
		byte SelfReadVRAM(int address)
		{
			return VRAM[address - VideoRAMStart];
		}
		/// <summary>
		/// The CPU would like to write to VRAM.
		/// This is ignored if the PPU is currently drawing pixels.
		/// </summary>
		/// <param name="address">Memory address to write to.</param>
		/// <param name="value">Value to write.</param>
		public void CPUWriteVRAM(int address, byte value)
		{
			if (Mode == Modes.DrawingPixels)
			{
				return;
			}
			VRAM[address - VideoRAMStart] = value;
		}
		/// <summary>
		/// The CPU would like to read OAM.
		/// </summary>
		/// <param name="address">Memory address to read from</param>
		/// <returns>Value at given address, or 0xff if the PPU is currently drawing pixels</returns>
		public byte CPUReadOAM(int address)
		{
			if (Mode == Modes.OAMScan || Mode == Modes.DrawingPixels)
			{
				return 0xff;
			}
			return OAM[address - ObjectAttributeMemoryStart];
		}
		/// <summary>
		/// Read from OAM, using a memory address
		/// </summary>
		/// <param name="address">Memory address to read from</param>
		/// <returns>Value at given address</returns>
		byte SelfReadOAM(int address)
		{
			return OAM[address - ObjectAttributeMemoryStart];
		}
		/// <summary>
		/// The CPU would like to write to OAM.
		/// This is ignored if the PPU is currently drawing pixels.
		/// </summary>
		/// <param name="address">Memory address to write to.</param>
		/// <param name="value">Value to write.</param>
		public void CPUWriteOAM(int address, byte value)
		{
			if (Mode == Modes.OAMScan || Mode == Modes.DrawingPixels)
			{
				return;
			}
			OAM[address - ObjectAttributeMemoryStart] = value;
		}
		public static bool AddressIsVRAM(ushort address)
		{
			return address >= VideoRAMStart && address <= VideoRAMEnd;
		}
		public static bool AddressIsOAM(ushort address)
		{
			return address >= ObjectAttributeMemoryStart && address <= ObjectAttributeMemoryEnd;
		}
	}
}
