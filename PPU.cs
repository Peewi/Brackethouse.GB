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

		const int BGPaletteAddress = 0xff47;
		const int Ob0PaletteAddress = 0xff48;
		const int Ob1PaletteAddress = 0xff49;

		const int ObjByteLength = 4;
		const int ObjOffsetX = 8;
		const int ObjOffsetY = 16;
		const int ObjWidth = 8;
		const int ObjBaseHeight = 8;

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
		int OffTicks = 0;
		ushort PreviousCPUTick = 0;
		public const int VRAMSize = 0x2000;
		byte[] VRAM = new byte[VRAMSize];
		public const int OAMSize = 40;
		public const int OAMSizeBytes = OAMSize * 4;
		byte[] OAM = new byte[OAMSizeBytes];
		public int Frame { get; private set; } = 0;

		byte LineObjectCount = 0;
		const int MaxObjectsPerLine = 10;
		byte[] LineObjects = new byte[MaxObjectsPerLine];
		/// <summary>
		/// Bit 0 of LCD Control.
		/// Controls whether the background is drawn.
		/// </summary>
		bool BGEnable;
		/// <summary>
		/// Bit 1 of LCD Control.
		/// Controls whether objects are drawn.
		/// </summary>
		bool ObjectEnable;
		/// <summary>
		/// Bit 2 of LCD Control.
		/// Controls whether objects are 8 or 16 pixels tall.
		/// </summary>
		bool ObjectSize;
		/// <summary>
		/// Bit 3 of LCD Control.
		/// Controls which tilemap is used for the background.
		/// </summary>
		bool BGTileMap;
		/// <summary>
		/// Bit 4 of LCD Control
		/// Controls which tiles are used for the background.
		/// </summary>
		bool BGTileSrc;
		/// <summary>
		/// Bit 5 of LCD Control.
		/// Controls whether the window gets drawn.
		/// </summary>
		bool WindowEnable;
		/// <summary>
		/// Bit 6 of LCD Control.
		/// Controls which tiles are used for the window.
		/// </summary>
		bool WindowTileMap;
		/// <summary>
		/// Bit 7 of LCD Control.
		/// Controls whether the display and PPU are on.
		/// </summary>
		bool LCDEnable;

		byte[] PaletteMask =
		[
			0b0000_0011,
			0b0000_1100,
			0b0011_0000,
			0b1100_0000,
		];

		Display Display;

		public PPU(IORegisters io, Display dsp)
		{
			IO = io;
			Display = dsp;
		}
		/// <summary>
		/// Call after each CPU instruction.
		/// </summary>
		/// <param name="tick">ticks from the CPU, for synchronization.</param>
		public void Step(ushort tick)
		{
			CheckLCDControl();
			if (!LCDEnable)
			{
				OffTicks += 4;
				Frame += OffTicks / (TicksPerLine * ScanLines);
				OffTicks %= (TicksPerLine * ScanLines);
				LineTicks = 0;
				PixelX = 0;
				PixelY = 0;
				Mode = Modes.HBlank;
				PreviousCPUTick = tick;
			}
			while (tick != PreviousCPUTick)
			{
				PreviousCPUTick++;
				Dot();
			}
			UpdateLCDStatus();
			IO[LYAddress] = PixelY;
		}
		/// <summary>
		/// PPU sub-step.
		/// </summary>
		void Dot()
		{
			// https://gbdev.io/pandocs/Rendering.html

			Modes prevMode = Mode;
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
				}
			}
			if (prevMode != Mode)
			{
				//https://gbdev.io/pandocs/STAT.html#ff41--stat-lcd-status
				if (Mode == Modes.HBlank && (IO[LCDStatusAddress] & 0b0000_1000) != 0)
				{
					FlagLCDStatusInterrupt();
				}
				else if (Mode == Modes.VBlank)
				{
					FlagVBlankInterrupt();
					if ((IO[LCDStatusAddress] & 0b0001_0000) != 0)
					{
						FlagLCDStatusInterrupt();
					}
				}
				else if (Mode == Modes.OAMScan && (IO[LCDStatusAddress] & 0b0010_0000) != 0)
				{
					FlagLCDStatusInterrupt();
				}
			}
		}
		/// <summary>
		/// Update the PPU Mode and LYC==LY bits of the LCD Status register.
		/// Also flags the LCD Status interrupt if appropriate.
		/// </summary>
		private void UpdateLCDStatus()
		{
			byte status = IO[LCDStatusAddress];
			byte oldLYCBit = status;
			oldLYCBit &= 0b0100;
			status &= 0b1111_1000;
			byte LYCBit = (byte)(PixelY == IO[LYCompareAddress] ? 0b0100 : 0);
			byte modeBits = (byte)((byte)Mode & 0b0011);
			status |= LYCBit;
			status |= modeBits;
			IO[LCDStatusAddress] = status;
			if (oldLYCBit != LYCBit && LYCBit != 0 && (status & 0b0100_0000) != 0)
			{
				FlagLCDStatusInterrupt();
			}
		}

		void CheckLCDControl()
		{
			byte lcdc = IO[LCDCAddress];
			BGEnable = (0b0000_0001 & lcdc) != 0;
			ObjectEnable = (0b0000_0010 & lcdc) != 0;
			ObjectSize = (0b0000_0100 & lcdc) != 0;
			BGTileMap = (0b0000_1000 & lcdc) != 0;
			BGTileSrc = (0b0001_0000 & lcdc) != 0;
			WindowEnable = (0b0010_0000 & lcdc) != 0;
			WindowTileMap = (0b0100_0000 & lcdc) != 0;
			LCDEnable = (0b1000_0000 & lcdc) != 0;
		}

		void DrawPixel(byte x, byte y)
		{
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

			byte tilemapPixel = 0;
			if (BGEnable)
			{
				tilemapPixel = ReadTilemapPixel(BGTileMap, BGTileSrc, tileNumber, tilePX, tilePY);
			}
			if (BGEnable && WindowEnable)
			{

			}
			byte objPixel = 0;
			if (ObjectEnable)
			{
				objPixel = ReadObjectPixel();
			}
			// Apply palette
			byte bgPalPixel = (byte)((IO[BGPaletteAddress] & PaletteMask[tilemapPixel]) >> (tilemapPixel * 2));

			byte finalPixel = objPixel == 0 ? bgPalPixel : objPixel;
			finalPixel &= 3;
			Display.SetPixel(PixelX, PixelY, finalPixel);
		}
		/// <summary>
		/// Check objects for current pixel and OAM scan and get a color index.
		/// </summary>
		/// <returns>Bits 0 and 1 are color index. Bits 4 and 7 are the palette and priority bits from the object properties.</returns>
		public byte ReadObjectPixel()
		{
			int height = ObjBaseHeight;
			if (ObjectSize)
			{
				height += ObjBaseHeight;
			}
			byte minX = byte.MaxValue;
			byte pixel = 0;
			byte pxAttrib = 0;
			for (int i = 0; i < LineObjectCount; i++)
			{
				byte objStart = (byte)(LineObjects[i] * ObjByteLength);
				byte y = OAM[objStart];
				byte x = OAM[objStart + 1];

				bool objectCoversPixel = PixelX >= x - ObjOffsetX && PixelX < x - ObjOffsetX + ObjWidth;
				if (objectCoversPixel && (pixel == 0 || x < minX))
				{
					minX = x;
					byte tileIndex = OAM[objStart + 2];
					byte flags = OAM[objStart + 3];
					byte priority = (byte)((flags & 0b1000_0000));
					byte yFlip = (byte)((flags & 0b0100_0000) >> 6);
					byte xFlip = (byte)((flags & 0b0010_0000) >> 5);
					byte palet = (byte)((flags & 0b0001_0000));

					byte tileX = (byte)(PixelX - (x - ObjOffsetX));
					byte tileY = (byte)(PixelY - (y - ObjOffsetY));
					tileX = (byte)((ObjWidth - 1) - tileX);
					tileX = (byte)Math.Abs((ObjWidth - 1) * xFlip - tileX);
					tileY = (byte)Math.Abs((height - 1) * yFlip - tileY);
					if (ObjectSize)
					{
						// For 16 pixel tall objects, control which object is read from.
						//https://gbdev.io/pandocs/OAM.html#byte-2--tile-index
						if (tileY < ObjBaseHeight)
						{
							tileIndex &= 0b1111_1110;
						}
						else
						{
							tileIndex |= 0b0000_0001;
						}
					}
					ushort tileStart = (ushort)(0x8000 + tileIndex * 16);

					byte bitMask = (byte)(1 << tileX);
					ushort byte1 = (ushort)(tileStart + tileY * 2);
					ushort byte2 = (ushort)(byte1 + 1);
					byte newpixel = (byte)((SelfReadVRAM(byte1) & bitMask) >> tileX);
					newpixel |= (byte)(((SelfReadVRAM(byte2) & bitMask) >> tileX) << 1);
					if (newpixel != 0)
					{
						pixel = newpixel;
						pxAttrib = 0;
						pxAttrib |= priority;
						pxAttrib |= palet;
					}
				}
			}
			pixel |= pxAttrib;
			return pixel;
		}
		byte ReadTilemapPixel(bool Altmap, bool altTiles, ushort mapTileIndex, byte x, byte y)
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
			ushort tileStart = (ushort)(0x9000 + (sbyte)tileIndex * 16);
			if (altTiles)
			{
				tileStart = (ushort)(0x8000 + tileIndex * 16);
			}
			x = (byte)((ObjWidth - 1) - x);
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
			LineObjectCount = 0;
			int objectHeight = ObjBaseHeight;
			if (ObjectSize)
			{
				objectHeight += ObjBaseHeight;
			}
			for (int i = 0; i < OAMSize; i++)
			{
				int yAddr = ObjectAttributeMemoryStart + ObjByteLength * i;
				byte y = (byte)(SelfReadOAM(yAddr) - ObjOffsetY);
				if (PixelY >= y && PixelY < y + objectHeight)
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
		void FlagLCDStatusInterrupt()
		{
			const ushort interruptFlag = 0xff0f;
			const int vBlankBit = 0b0000_0010;
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
					tilePX = (ObjWidth - 1) - tilePX;
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
			if (Mode == Modes.DrawingPixels && LCDEnable)
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
			if (Mode == Modes.DrawingPixels && LCDEnable)
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
			if ((Mode == Modes.OAMScan || Mode == Modes.DrawingPixels) && LCDEnable)
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
			if ((Mode == Modes.OAMScan || Mode == Modes.DrawingPixels) && LCDEnable)
			{
				return;
			}
			OAM[address - ObjectAttributeMemoryStart] = value;
		}
		/// <summary>
		/// Used for DMA writing to OAM.
		/// </summary>
		/// <param name="index">Index to the OAM array. NOT a memory address.</param>
		/// <param name="value">Byte to write.</param>
		public void DMAWriteOAM(int index, byte value)
		{
			OAM[index] = value;
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
