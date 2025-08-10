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
		readonly Color[] DisplayColors = [
			new Color(0xff,0xff,0xff),
			new Color(0xd3,0xd3,0xd3),
			new Color(0xa9,0xa9,0xa9),
			new Color(0x00,0x00,0x00),
			];
		const ushort VideoRAMStart = 0x8000;
		const ushort VideoRAMEnd = 0x9FFF;
		const ushort ObjectAttributeMemoryStart = 0xFE00;
		const ushort ObjectAttributeMemoryEnd = 0xFE9F;

		const int BGPaletteAddress = 0xff47;
		const int Ob0PaletteAddress = 0xff48;
		const int Ob1PaletteAddress = 0xff49;

		protected const int ObjByteLength = 4;
		protected const int ObjOffsetX = 8;
		protected const int ObjOffsetY = 16;
		protected const int ObjWidth = 8;
		protected const int ObjBaseHeight = 8;

		protected readonly IORegisters IO;
		Modes Mode = Modes.OAMScan;
		protected byte PixelX;
		protected byte PixelY;
		bool WindowFrameEnable = false;
		public const byte Width = 160;
		public const byte Height = 144;
		const byte ScanLines = 154;
		const int TicksPerLine = 456;
		public const int TicksPerFrame = TicksPerLine * ScanLines;
		public const int LCDCAddress = 0xff40;
		public const int LCDStatusAddress = 0xff41;
		public const ushort LYAddress = 0xff44;
		public const ushort LYCompareAddress = 0xff45;
		int LineTicks = 0;
		int OffTicks = 0;
		public const int VRAMSize = 0x2000;
		protected byte[] VRAM = new byte[VRAMSize];
		public const int OAMSize = 40;
		public const int OAMSizeBytes = OAMSize * 4;
		protected byte[] OAM = new byte[OAMSizeBytes];
		public int Frame { get; private set; } = 0;

		protected byte LineObjectCount = 0;
		protected const int MaxObjectsPerLine = 10;
		protected byte[] LineObjects = new byte[MaxObjectsPerLine];
		byte BackgroundPalette = 0;
		byte[] ObjectPalette = [0, 0];
		/// <summary>
		/// Bit 0 of LCD Control (on DMG). 
		/// Controls whether the background is drawn.
		/// </summary>
		protected bool BGEnable;
		/// <summary>
		/// Whether to prioritize background over objects. Only for CGB.
		/// </summary>
		protected bool BGPriority;
		/// <summary>
		/// Bit 1 of LCD Control.
		/// Controls whether objects are drawn.
		/// </summary>
		bool ObjectEnable;
		/// <summary>
		/// Bit 2 of LCD Control.
		/// Controls whether objects are 8 or 16 pixels tall.
		/// </summary>
		protected bool ObjectSize;
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
		public void Step(ushort ticks)
		{
			CheckLCDControl();
			if (!LCDEnable)
			{
				OffTicks += ticks;
				Frame += OffTicks / TicksPerFrame;
				OffTicks %= TicksPerFrame;
				LineTicks = 0;
				PixelX = 0;
				PixelY = 0;
				Mode = Modes.HBlank;
			}
			ReadIO();
			for (int i = 0; i < ticks; i++)
			{
				Dot();
			}
			UpdateLCDStatus();
			IO[LYAddress] = PixelY;
		}
		/// <summary>
		/// Read some specific values from IO.
		/// </summary>
		protected virtual void ReadIO()
		{
			BackgroundPalette = IO[BGPaletteAddress];
			ObjectPalette[0] = IO[Ob0PaletteAddress];
			ObjectPalette[1] = IO[Ob1PaletteAddress];
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
				if (PixelY >= ScanLines)
				{
					WindowFrameEnable = false;
				}
				PixelY %= ScanLines;
				Mode = PixelY >= Height ? Modes.VBlank : Mode;
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

		protected virtual void CheckLCDControl()
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

			ColorIndex bgPixel = new(0, 0, Layer.Background, false);
			if (BGEnable)
			{
				bgPixel = ReadTilemapPixel(BGTileMap, BGTileSrc, tilemapPosX, tilemapPosY);
				if (WindowEnable && wx >= 0 && wx <=166 && wy >= 0 && wy <= 143)
				{
					const byte WindowXOffset = 7;
					WindowFrameEnable |= wy == y;
					if (WindowFrameEnable && wx <= x + WindowXOffset)
					{
						byte windowDrawX = x;
						windowDrawX -= wx;
						windowDrawX += WindowXOffset;
						byte windowDrawY = y;
						windowDrawY -= wy;
						bgPixel = ReadTilemapPixel(WindowTileMap, BGTileSrc, windowDrawX, windowDrawY);
					}
				}
			}
			ColorIndex objPixel = new ColorIndex();
			if (ObjectEnable)
			{
				objPixel = ReadObjectPixel();
			}
			Color pixelColor = GetColor(PrioritySelect(objPixel, bgPixel, BGPriority));
			Display.SetPixel(PixelX, PixelY, pixelColor);
		}
		/// <summary>
		/// Determine whether to draw background or object.
		/// </summary>
		/// <param name="obj">Object color index (with priority)</param>
		/// <param name="bg">Background color index (with priority)</param>
		/// <param name="priorityBit">LCDC bit 0. Only used in color mode</param>
		/// <returns>The color index to draw.</returns>
		protected virtual ColorIndex PrioritySelect(ColorIndex obj, ColorIndex bg, bool priorityBit)
		{
			if (obj.BackgroundPriority && bg.Index > 0)
			{
				return bg;
			}
			if (obj.Index > 0)
			{
				return obj;
			}
			return bg;
		}
		/// <summary>
		/// Get a color from the color palettes.
		/// </summary>
		/// <param name="index">Where to look.</param>
		/// <returns>A color</returns>
		protected virtual Color GetColor(ColorIndex index)
		{
			byte palette;
			if (index.Layer == Layer.Background)
			{
				palette = BackgroundPalette;
			}
			else
			{
				palette = ObjectPalette[index.Palette];
			}
			int finalIndex = (palette & PaletteMask[index.Index]) >> (index.Index * 2);
			return DisplayColors[finalIndex];
		}
		/// <summary>
		/// Check objects for current pixel and OAM scan and get a color index.
		/// </summary>
		/// <returns>Bits 0 and 1 are color index. Bits 4 and 7 are the palette and priority bits from the object properties.</returns>
		protected virtual ColorIndex ReadObjectPixel()
		{
			int height = ObjBaseHeight;
			if (ObjectSize)
			{
				height += ObjBaseHeight;
			}
			byte minX = byte.MaxValue;
			ColorIndex pixelIndex = new ColorIndex(0, 0, Layer.Object, false);
			for (int i = 0; i < LineObjectCount; i++)
			{
				byte objStart = (byte)(LineObjects[i] * ObjByteLength);
				byte y = OAM[objStart];
				byte x = OAM[objStart + 1];

				bool objectCoversPixel = PixelX >= x - ObjOffsetX && PixelX < x - ObjOffsetX + ObjWidth;
				if (objectCoversPixel && (pixelIndex.Index == 0 || x < minX))
				{
					minX = x;
					byte tileIndex = OAM[objStart + 2];
					byte flags = OAM[objStart + 3];
					byte priority = (byte)(flags & 0b1000_0000);
					byte yFlip = (byte)((flags & 0b0100_0000) >> 6);
					byte xFlip = (byte)((flags & 0b0010_0000) >> 5);
					byte palet = (byte)(flags & 0b0001_0000); // DMG palette
					palet >>= 4;

					byte tileX = (byte)(PixelX - (x - ObjOffsetX));
					byte tileY = (byte)(PixelY - (y - ObjOffsetY));
					tileX = (byte)((ObjWidth - 1) - tileX);
					tileX = (byte)Math.Abs((ObjWidth - 1) * xFlip - tileX);
					tileY = (byte)Math.Abs((height - 1) * yFlip - tileY);
					if (ObjectSize)
					{
						// For 16 pixel tall objects, control which object is read from.
						//https://gbdev.io/pandocs/OAM.html#byte-2--tile-index
						tileIndex &= 0b1111_1110;
					}
					ushort tileStart = (ushort)(0x8000 + tileIndex * 16);

					byte bitMask = (byte)(1 << tileX);
					ushort byte1 = (ushort)(tileStart + tileY * 2);
					ushort byte2 = (ushort)(byte1 + 1);
					byte newpixel = (byte)((SelfReadVRAM(byte1) & bitMask) >> tileX);
					newpixel |= (byte)(((SelfReadVRAM(byte2) & bitMask) >> tileX) << 1);
					if (newpixel != 0)
					{
						pixelIndex = new ColorIndex(newpixel, palet, Layer.Object, priority != 0);
					}
				}
			}
			return pixelIndex;
		}
		/// <summary>
		/// Read a pixel from a tilemap
		/// </summary>
		/// <param name="Altmap">Whether to read the alternate tile map</param>
		/// <param name="altTiles">Whether to pull from the alternate tile data</param>
		/// <param name="mapX">X position for where to read from</param>
		/// <param name="mapY">Y position for where to read from</param>
		/// <returns>A 2-bit color read from the tilemap. Palette has NOT been applied.</returns>
		protected virtual ColorIndex ReadTilemapPixel(bool Altmap, bool altTiles, byte mapX, byte mapY)
		{
			const byte tilesizePx = 8;
			byte tileX = mapX;
			tileX /= tilesizePx;
			byte tileY = mapY;
			tileY /= tilesizePx;

			const byte mapWidth = 32;
			ushort tileNumber = (ushort)(tileY * mapWidth + tileX);
			byte tilePX = mapX;
			tilePX %= tilesizePx;
			byte tilePY = mapY;
			tilePY %= tilesizePx;

			const ushort map1Addr = 0x9800;
			const ushort map2Addr = 0x9C00;
			ushort tAddr = map1Addr;
			if (Altmap)
			{
				tAddr = map2Addr;
			}
			tAddr += tileNumber;
			byte tileIndex = SelfReadVRAM(tAddr);
			ushort tileStart = (ushort)(0x9000 + (sbyte)tileIndex * 16);
			if (altTiles)
			{
				tileStart = (ushort)(0x8000 + tileIndex * 16);
			}
			tilePX = (byte)((ObjWidth - 1) - tilePX);
			byte bitMask = (byte)(1 << tilePX);
			ushort byte1 = (ushort)(tileStart + tilePY * 2);
			ushort byte2 = (ushort)(byte1 + 1);
			byte data = (byte)((SelfReadVRAM(byte1) & bitMask) >> tilePX);
			data |= (byte)(((SelfReadVRAM(byte2) & bitMask) >> tilePX) << 1);
			return new ColorIndex(data, 0, Layer.Background, false);
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
				int y = SelfReadOAM(yAddr) - ObjOffsetY;
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

					//Display.SetPixel((byte)x, (byte)y, data);
				}
			}
			Display.Output();
		}
		/// <summary>
		/// The CPU would like to read VRAM.
		/// </summary>
		/// <param name="address">Memory address to read from</param>
		/// <returns>Value at given address, or 0xff if the PPU is currently drawing pixels</returns>
		virtual public byte CPUReadVRAM(int address)
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
		virtual protected byte SelfReadVRAM(int address)
		{
			return VRAM[address - VideoRAMStart];
		}
		/// <summary>
		/// The CPU would like to write to VRAM.
		/// This is ignored if the PPU is currently drawing pixels.
		/// </summary>
		/// <param name="address">Memory address to write to.</param>
		/// <param name="value">Value to write.</param>
		virtual public void CPUWriteVRAM(int address, byte value)
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
		protected enum Layer
		{
			Object,
			Background
		}
		/// <summary>
		/// Used to look up a color in the palettes
		/// </summary>
		/// <param name="index">Color index</param>
		/// <param name="palette">Palette index</param>
		/// <param name="layer">Whether it's object or background</param>
		/// <param name="priority">Whether to prioritize background</param>
		protected struct ColorIndex(byte index, byte palette, PPU.Layer layer, bool priority)
		{
			public byte Index = index;
			public byte Palette = palette;
			public Layer Layer = layer;
			public bool BackgroundPriority = priority;
		}
	}
}
