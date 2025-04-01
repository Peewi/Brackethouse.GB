using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brackethouse.GB
{
	class Memory
	{
		const int MemorySize = ushort.MaxValue + 1;
		byte[] Bytes = new byte[MemorySize];
		Cartridge Cart;
		PPU Graphics;
		IORegisters IO;
		const ushort ROMStart = 0x0000;
		const ushort ROMBank1End = 0x3FFF;
		const ushort SwitchableBank = 0x4000;
		const ushort SwitchableBankEnd = 0x7FFF;
		const ushort VideoRAM = 0x8000;
		const ushort ExternalRAM = 0xA000;
		const ushort WorkRAM = 0xC000;
		const ushort EchoRAM = 0xE000;
		const ushort EchoRAMEnd = 0xFDFF;
		const ushort ObjectAttributeMemory = 0xFE00;
		const ushort ObjectAttributeMemoryEnd = 0xFE9F;
		const ushort NotUsable = 0xFEA0;
		const ushort IORegistersStart = 0xFF00;
		const ushort HighRAM = 0xFF80;
		const ushort InterruptEnableRegister = 0xFFFF;
		ushort PreviousCPUTick = 0;
		int DIVTimer = 0;
		int TIMATimer = 0;
		public Memory(Cartridge cart, PPU gfx, IORegisters io)
		{
			Cart = cart;
			Graphics = gfx;
			IO = io;
		}
		public Byte this[int i]
		{
			get => Read(i);
			set => Write(i, value);
		}
		/// <summary>
		/// Read from memory. For use by CPU.
		/// </summary>
		/// <param name="address">Address to read</param>
		/// <returns>Byte that was read.</returns>
		public byte Read(int address)
		{
			ushort uadr = (ushort)address;
			if (address <= SwitchableBankEnd)
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
			return Bytes[address];
		}
		/// <summary>
		/// Write to memory. For use by CPU.
		/// </summary>
		/// <param name="address">Address to write to.</param>
		/// <param name="value">Byte to write.</param>
		public void Write(int address, byte value)
		{
			ushort uadr = (ushort)address;
			if (address <= SwitchableBankEnd)
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
				return;
			}
			Bytes[address] = value;
		}
	}
}
