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
		const ushort IORegisters = 0xFF00;
		const ushort HighRAM = 0xFF80;
		const ushort InterruptEnableRegister = 0xFFFF;
		ushort PreviousCPUTick = 0;
		int DIVTimer = 0;
		int TIMATimer = 0;
		public Memory(Cartridge cart)
		{
			Cart = cart;
		}
		public Byte this[int i]
		{
			get => Read(i);
			set => Write(i, value);
		}
		public byte Read(int address)
		{
			if (address <= SwitchableBankEnd)
			{
				return Cart.Read((ushort)address);
			}
			return Bytes[address];
		}
		public void Write(int address, byte value)
		{
			if (address <= SwitchableBankEnd)
			{
				Cart.Write((ushort)address, value);
				return;
			}
			if (address >= VideoRAM && address < ExternalRAM)
			{

			}
			if (address >= IORegisters)
			{

			}
			Bytes[address] = value;
		}
		public void StepTimerRegisters(ushort tick)
		{
			// https://gbdev.io/pandocs/Timer_and_Divider_Registers.html
			int ticks = PreviousCPUTick - tick;
			if (ticks < 0)
			{
				ticks += ushort.MaxValue;
			}

			const ushort divider = 0xff04;
			const ushort timerCounter = 0xff05;
			const ushort timerMod = 0xff06;
			const ushort timerControl = 0xff07;
			bool TACEnable = (Bytes[timerControl] & 0b00_00_01_00) != 0;
			byte TACClockSelect = (byte)(Bytes[timerControl] & 0b00_00_00_11);
			// Each M-Cycle is 4 ticks
			int[] ticksPerInc = [256 * 4, 4 * 4, 16 * 4, 64 * 4];
			// DIV always counts up
			DIVTimer += ticks;
			int newDiv = Bytes[divider] + DIVTimer / ticksPerInc[3];
			DIVTimer %= ticksPerInc[3];
			Bytes[divider] = (byte)newDiv;
			// TIMA only counts up when enabled.
			if (TACEnable)
			{
				TIMATimer += ticks;
				int newCount = Bytes[timerCounter] + TIMATimer / ticksPerInc[TACClockSelect];
				TIMATimer %= ticksPerInc[TACClockSelect];
				if (newCount > 0xff)
				{
					newCount = Bytes[timerMod];
					// TODO: Do an interrupt.
					const ushort interruptFlag = 0xff0f;
					const int timerBit = 0b0000_0100;
					this[interruptFlag] |= timerBit;
				}
				Bytes[timerCounter] = (byte)newCount;
			}

			PreviousCPUTick = tick;
		}
	}
}
