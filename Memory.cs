using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brackethouse.GB
{
	class Memory
	{
		const int WRAMBankSize = 0x1000;
		const int HRAMSize = 0x80;
		const ushort WRAMBankSwitchAddress = 0xff70;
		GameBoyType Mode;
		int WRAMBank = 1;
		byte[] WorkRAM = new byte[WRAMBankSize * 2];
		byte[] HighRAM = new byte[HRAMSize];
		Cartridge Cart;
		PPU Graphics;
		IORegisters IO;
		const ushort ROMStart = 0x0000;
		const ushort ROMBank1End = 0x3FFF;
		const ushort SwitchableBank = 0x4000;
		const ushort SwitchableBankEnd = 0x7FFF;
		const ushort VideoRAM = 0x8000;
		const ushort ExternalRAM = 0xA000;
		const ushort WorkRAMStart = 0xC000;
		const ushort EchoRAM = 0xE000;
		const ushort EchoRAMEnd = 0xFDFF;
		const ushort ObjectAttributeMemory = 0xFE00;
		const ushort ObjectAttributeMemoryEnd = 0xFE9F;
		const ushort NotUsable = 0xFEA0;
		const ushort IORegistersStart = 0xFF00;
		const ushort HighRAMStart = 0xFF80;
		const ushort InterruptEnableRegister = 0xFFFF;

		const ushort HDMA1Address = 0xff51;
		const ushort HDMA2Address = 0xff52;
		const ushort HDMA3Address = 0xff53;
		const ushort HDMA4Address = 0xff54;
		const ushort HDMA5Address = 0xff55;
		bool HDMAInProgress = false;
		bool HDMATransferThisBlank = false;
		public Memory(Cartridge cart, PPU gfx, IORegisters io, GameBoyType mode)
		{
			Cart = cart;
			Graphics = gfx;
			IO = io;
			Mode = mode;
			if (mode == GameBoyType.GameBoyColor)
			{
				WorkRAM = new byte[WRAMBankSize * 8];
			}
		}
		public Byte this[int i]
		{
			get => Read(i);
			set => Write(i, value);
		}
		/// <summary>
		/// HDMA takes time.
		/// </summary>
		public void StepDMA()
		{
			if (!HDMAInProgress)
			{
				return;
			}
			bool isHBlank = Graphics.Mode == PPUMode.HBlank;
			if (!isHBlank || HDMATransferThisBlank)
			{
				return;
			}
			ushort src = (byte)(IO[HDMA1Address] << 8 + IO[HDMA2Address]);
			ushort dest = (byte)(IO[HDMA3Address] << 8 + IO[HDMA4Address]);
			const ushort srcMask = 0xfff0;
			src &= srcMask;
			const ushort destMask = 0x1ff0;
			const ushort destStart = 0x8000;
			dest &= destMask;
			dest += destStart;

			ushort length = IO[HDMA5Address];
			const byte lengthMask = 0x7f;
			length &= lengthMask;
			length++;
			length *= 0x10;
			const ushort HDMAStepSize = 0x10;
			for (ushort i = 0; i < HDMAStepSize; i++)
			{
				ushort s = src;
				s += length;
				s -= i;
				s--;
				ushort d = dest;
				d += length;
				d -= i;
				d--;
				this[d] = this[s];
			}
			HDMATransferThisBlank = true;
			byte hdmabyte = IO[HDMA5Address];
			hdmabyte--;
			HDMAInProgress = hdmabyte != 0xff;
			IO[HDMA5Address] = hdmabyte;
		}
		/// <summary>
		/// Read from memory. For use by CPU.
		/// </summary>
		/// <param name="address">Address to read</param>
		/// <returns>Byte that was read.</returns>
		public byte Read(int address)
		{
			ushort uadr = (ushort)address;
			if (Cartridge.AddressIsCartridge(uadr))
			{
				return Cart.Read(uadr);
			}
			if (PPU.AddressIsVRAM(uadr))
			{
				return Graphics.CPUReadVRAM(uadr);
			}
			if (PPU.AddressIsOAM(uadr))
			{
				return Graphics.CPUReadOAM(uadr);
			}
			if (IORegisters.AddressIsIO(uadr))
			{
				return IO[uadr];
			}
			if (AddressIsHRAM(uadr))
			{
				int hramIndex = uadr - HighRAMStart;
				return HighRAM[hramIndex];
			}
			if (AddressIsEchoRAM(uadr))
			{
				uadr -= 0x2000;
			}
			if (AddressIsNotUsable(uadr))
			{
				return 0xff;
			}
			return WorkRAM[WRAMIndex(uadr)];
		}
		/// <summary>
		/// Write to memory. For use by CPU.
		/// </summary>
		/// <param name="address">Address to write to.</param>
		/// <param name="value">Byte to write.</param>
		public void Write(int address, byte value)
		{
			ushort uadr = (ushort)address;
			if (Cartridge.AddressIsCartridge(uadr))
			{
				Cart.Write(uadr, value);
				return;
			}
			if (PPU.AddressIsVRAM(uadr))
			{
				Graphics.CPUWriteVRAM(uadr, value);
				return;
			}
			if (PPU.AddressIsOAM(uadr))
			{
				Graphics.CPUWriteOAM(uadr, value);
				return;
			}
			if (IORegisters.AddressIsIO(uadr))
			{
				IO.CPUWrite(uadr, value);
				//
				if (address == WRAMBankSwitchAddress && Mode == GameBoyType.GameBoyColor)
				{
					int newBank = value & 0x07;
					newBank = Math.Clamp(newBank, 1, 8);
					WRAMBank = newBank;
				}
				else if (address == IORegisters.OAMDMAAddress)
				{
					OAM_DMA(value);
				}
				else if (address == HDMA5Address
					&& Mode == GameBoyType.GameBoyColor)
				{
					StartHDMA();
				}
				return;
			}
			if (AddressIsHRAM(uadr))
			{
				int hramIndex = uadr - HighRAMStart;
				HighRAM[hramIndex] = value;
				return;
			}
			if (AddressIsEchoRAM(uadr))
			{
				uadr -= 0x2000;
			}
			if (AddressIsNotUsable(uadr))
			{
				return;
			}
			WorkRAM[WRAMIndex(uadr)] = value;
		}
		/// <summary>
		/// Do this dumb things where CGB copies to VRAM.
		/// </summary>
		public void StartHDMA()
		{
			// https://gbdev.io/pandocs/CGB_Registers.html#lcd-vram-dma-transfers
			byte hdma = IO[HDMA5Address];
			const byte lengthMask = 0x7f;
			byte bit7 = 0x80;
			bool hblank = (hdma & bit7) != 0;

			if (!hblank)
			{
				ushort src = (byte)(IO[HDMA1Address] << 8 + IO[HDMA2Address]);
				ushort dest = (byte)(IO[HDMA3Address] << 8 + IO[HDMA4Address]);
				const ushort srcMask = 0xfff0;
				src &= srcMask;
				const ushort destMask = 0x1ff0;
				const ushort destStart = 0x8000;
				dest &= destMask;
				dest += destStart;

				ushort length = hdma;
				length &= lengthMask;
				length++;
				length *= 0x10;
				// Fuck it, I'm cheating and doing it all at once.
				for (ushort i = 0; i < length; i++)
				{
					ushort s = src;
					s += i;
					ushort d = dest;
					d += i;
					this[d] = this[s];
				}
				IO[HDMA5Address] = 0xff;
				HDMAInProgress = false;
				return;
			}
			// Turn on HBlank DMA.
			IO[HDMA5Address] &= lengthMask;
			HDMAInProgress = true;
			HDMATransferThisBlank = Graphics.Mode == PPUMode.HBlank;
		}
		/// <summary>
		/// Whether an address is High RAM
		/// </summary>
		/// <param name="address">Game Boy memory address</param>
		/// <returns>Whether it's High RAM</returns>
		static bool AddressIsHRAM(ushort address)
		{
			return address >= HighRAMStart;
		}
		/// <summary>
		/// Whether an address is echo RAM
		/// </summary>
		/// <param name="address">Game Boy memory address</param>
		/// <returns>Whether it's echo RAM</returns>
		static bool AddressIsEchoRAM(ushort address)
		{
			return address >= EchoRAM && address <= EchoRAMEnd;
		}
		/// <summary>
		/// Whether an address is in the unusable space.
		/// </summary>
		/// <param name="address">Game Boy memory address</param>
		/// <returns>Whether it's unusable</returns>
		static bool AddressIsNotUsable(ushort address)
		{
			return address >= NotUsable && address < IORegistersStart;
		}
		/// <summary>
		/// Get index to work RAM array.
		/// </summary>
		/// <param name="address">Game Boy memory address</param>
		/// <returns>Index to work RAM array.</returns>
		int WRAMIndex(ushort address)
		{
			int wramIndex = address - WorkRAMStart;
			if (wramIndex >= WRAMBankSize)
			{
				wramIndex += (WRAMBank - 1) * WRAMBankSize;
			}
			return wramIndex;
		}
		/// <summary>
		/// https://gbdev.io/pandocs/OAM_DMA_Transfer.html
		/// On real hardware this takes 160 M-cycles. Here I'm cheating and doing it instantly.
		/// </summary>
		/// <param name="value">Byte written to OAM DMA register</param>
		void OAM_DMA(byte value)
		{
			ushort DMABaseAddr = (ushort)(value << 8);
			for (int i = 0; i < PPU.OAMSizeBytes; i++)
			{
				int readAddr = DMABaseAddr + i;
				Graphics.DMAWriteOAM(i, this[readAddr]);
			}
		}
	}
}
