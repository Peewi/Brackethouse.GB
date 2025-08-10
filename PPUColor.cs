using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brackethouse.GB
{
	internal class PPUColor : PPU
	{
		GameBoyType CompatibilityMode;
		int VRAMBank = 0;
		int ObjectPriorityMode = 0;
		byte[] BackgroundPaletteMemory = new byte[8 * 4 * 2];
		byte[] ObjectPaletteMemory = new byte[8 * 4 * 2];
		const ushort VRAMBankAddress = 0xff4f;
		const ushort BGPalIndex = 0xff68;
		const ushort BGPalData = 0xff69;
		const ushort ObjPalIndex = 0xff6a;
		const ushort ObjPalData = 0xff6b;
		const ushort ObjPriorityAddress = 0xff6c;
		public PPUColor(IORegisters io, Display dsp, GameBoyType compatMode) : base(io, dsp)
		{
			CompatibilityMode = compatMode;
			if (CompatibilityMode != GameBoyType.GameBoyColor)
			{
				SetCompatibilityPalettes();
				return;
			}
			VRAM = new byte[VRAMSize * 2];
			for (int i = 0; i < BackgroundPaletteMemory.Length; i++)
			{
				BackgroundPaletteMemory[i] = 0xff;
			}
		}
		void SetCompatibilityPalettes()
		{
			// TODO: proper palette support for DMG game on CGB
			// for 8 palettes
			for (int i = 0; i < 8; i++)
			{
				BackgroundPaletteMemory[i * 8 + 0] = 0xff;
				BackgroundPaletteMemory[i * 8 + 1] = 0xff;
				BackgroundPaletteMemory[i * 8 + 2] = 0x5a;
				BackgroundPaletteMemory[i * 8 + 3] = 0x6b;
				BackgroundPaletteMemory[i * 8 + 4] = 0xb5;
				BackgroundPaletteMemory[i * 8 + 5] = 0x56;
				BackgroundPaletteMemory[i * 8 + 6] = 0x00;
				BackgroundPaletteMemory[i * 8 + 7] = 0x00;

				ObjectPaletteMemory[i * 8 + 0] = 0xff;
				ObjectPaletteMemory[i * 8 + 1] = 0xff;
				ObjectPaletteMemory[i * 8 + 2] = 0x5a;
				ObjectPaletteMemory[i * 8 + 3] = 0x6b;
				ObjectPaletteMemory[i * 8 + 4] = 0xb5;
				ObjectPaletteMemory[i * 8 + 5] = 0x56;
				ObjectPaletteMemory[i * 8 + 6] = 0x00;
				ObjectPaletteMemory[i * 8 + 7] = 0x00;
			}
		}
		/// <summary>
		/// Read some specific values from IO.
		/// Different ones in color mode.
		/// </summary>
		protected override void ReadIO()
		{
			if (CompatibilityMode != GameBoyType.GameBoyColor)
			{
				base.ReadIO();
				return;
			}
			VRAMBank = IO[VRAMBankAddress] & 0x01;
			ObjectPriorityMode = IO[ObjPriorityAddress] & 0x01;
			// https://gbdev.io/pandocs/Palettes.html#lcd-color-palettes-cgb-only
			if (IO.WrittenAddress == BGPalData)
			{
				byte index = IO[BGPalIndex];
				bool increment = (index & 0x80) != 0;
				index &= 0x3f;
				BackgroundPaletteMemory[index] = IO[BGPalData];
				if (increment)
				{
					index++;
					index %= 64;
					index |= 0x80;
					IO[BGPalIndex] = index;
				}
			}
			else if (IO.WrittenAddress == ObjPalData)
			{
				byte index = IO[ObjPalIndex];
				bool increment = (index & 0x80) != 0;
				index &= 0x3f;
				byte palDat = IO[ObjPalData];
				ObjectPaletteMemory[index] = palDat;
				if (increment)
				{
					index++;
					index %= 64;
					index |= 0x80;
					IO[ObjPalIndex] = index;
				}
			}
		}
		protected override void CheckLCDControl()
		{
			base.CheckLCDControl();
			BGPriority = BGEnable;
			BGEnable = true;
		}
		protected override ColorIndex ReadTilemapPixel(bool Altmap, bool altTiles, byte mapX, byte mapY)
		{
			if (CompatibilityMode != GameBoyType.GameBoyColor)
			{
				return base.ReadTilemapPixel(Altmap, altTiles, mapX, mapY);
			}
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
			TileProperties prop = new TileProperties(SelfReadVRAM(tAddr + VRAMSize));
			byte tileIndex = SelfReadVRAM(tAddr);
			ushort tileStart = (ushort)(0x9000 + (sbyte)tileIndex * 16);
			if (altTiles)
			{
				tileStart = (ushort)(0x8000 + tileIndex * 16);
			}
			if (!prop.XFlip)
			{
				tilePX = (byte)((ObjWidth - 1) - tilePX);
			}
			if (prop.YFlip)
			{
				tilePY = (byte)((ObjWidth - 1) - tilePY);
			}
			byte bitMask = (byte)(1 << tilePX);
			ushort byte1 = (ushort)(tileStart + tilePY * 2 + prop.Bank * VRAMSize);
			ushort byte2 = (ushort)(byte1 + 1);
			byte data = (byte)((SelfReadVRAM(byte1) & bitMask) >> tilePX);
			data |= (byte)(((SelfReadVRAM(byte2) & bitMask) >> tilePX) << 1);
			return new ColorIndex(data, prop.Palette, Layer.Background, prop.Priority);
		}
		protected override ColorIndex ReadObjectPixel()
		{
			if (CompatibilityMode != GameBoyType.GameBoyColor)
			{
				return base.ReadObjectPixel();
			}
			int height = ObjBaseHeight;
			if (ObjectSize)
			{
				height += ObjBaseHeight;
			}
			ColorIndex pixelIndex = new ColorIndex(0, 0, Layer.Object, false);
			for (int i = 0; i < LineObjectCount; i++)
			{
				byte objStart = (byte)(LineObjects[i] * ObjByteLength);
				byte y = OAM[objStart];
				byte x = OAM[objStart + 1];

				bool objectCoversPixel = PixelX >= x - ObjOffsetX && PixelX < x - ObjOffsetX + ObjWidth;
				if (objectCoversPixel && pixelIndex.Index == 0)
				{
					byte tileIndex = OAM[objStart + 2];
					byte flags = OAM[objStart + 3];
					byte priority = (byte)(flags & 0b1000_0000);
					byte yFlip = (byte)((flags & 0b0100_0000) >> 6);
					byte xFlip = (byte)((flags & 0b0010_0000) >> 5);
					byte bank = (byte)(flags & 0b0000_1000);
					bank >>= 3;
					byte palet = (byte)(flags & 0b0000_0111); // CGB palette

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
		protected override ColorIndex PrioritySelect(ColorIndex obj, ColorIndex bg, bool priorityBit)
		{
			if (CompatibilityMode != GameBoyType.GameBoyColor)
			{
				return base.PrioritySelect(obj, bg, priorityBit);
			}
			if (obj.Index == 0)
			{
				return bg;
			}
			// https://gbdev.io/pandocs/Tile_Maps.html#bg-to-obj-priority-in-cgb-mode
			if (!priorityBit)
			{
				return obj;
			}
			if (!obj.BackgroundPriority && ! bg.BackgroundPriority)
			{
				return obj;
			}
			if (bg.Index == 0)
			{
				return obj;
			}
			return bg;
		}
		protected override Color GetColor(ColorIndex index)
		{
			byte[] paletteMem;
			if (index.Layer == Layer.Background)
			{
				paletteMem = BackgroundPaletteMemory;
			}
			else
			{
				paletteMem = ObjectPaletteMemory;
			}
			int palStart = index.Palette * 4 * 2;
			int colStart = palStart + index.Index * 2;
			ushort colDat = paletteMem[colStart];
			colDat += (ushort)(paletteMem[colStart + 1] << 8);
			return Color.FromRGB555(colDat);
		}
		protected override byte SelfReadVRAM(int address)
		{
			if (CompatibilityMode != GameBoyType.GameBoyColor)
			{
				return base.SelfReadVRAM(address);
			}
			//address += VRAMBank * VRAMSize;
			return base.SelfReadVRAM(address);
		}
		public override byte CPUReadVRAM(int address)
		{
			if (CompatibilityMode != GameBoyType.GameBoyColor)
			{
				return base.CPUReadVRAM(address);
			}
			address += VRAMBank * VRAMSize;
			return base.CPUReadVRAM(address);
		}
		public override void CPUWriteVRAM(int address, byte value)
		{
			if (CompatibilityMode != GameBoyType.GameBoyColor)
			{
				base.CPUWriteVRAM(address, value);
			}
			address += VRAMBank * VRAMSize;
			base.CPUWriteVRAM(address, value);
		}
		struct TileProperties
		{
			public bool Priority;
			public bool YFlip;
			public bool XFlip;
			public byte Bank;
			public byte Palette;
			public TileProperties(byte val)
			{
				Palette = val;
				Palette &= 0x07;
				Bank = val;
				Bank >>= 3;
				Bank &= 1;
				XFlip = (val & 0x20) != 0;
				YFlip = (val & 0x40) != 0;
				Priority = (val & 0x80) != 0;
			}
		}
	}
}
