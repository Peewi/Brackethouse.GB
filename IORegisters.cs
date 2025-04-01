using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brackethouse.GB
{
    class IORegisters
	{
		const ushort IORegistersStart = 0xFF00;
		const ushort IORegistersEnd = 0xFF7F;
		byte[] IOMem = new byte[0x80];
		ushort PreviousCPUTick = 0;
		int DIVTimer = 0;
		int TIMATimer = 0;
		public byte this[int i]
		{
			get => IOMem[i - IORegistersStart];
			set => IOMem[i - IORegistersStart] = value;
		}
		public void CPUWrite(int address, byte value)
		{
			if (address == PPU.LYAddress)
			{
				// LCD Y Coordinate is read only.
				return;
			}
			if (address == PPU.LCDStatusAddress)
			{
				// The lower 3 bits of LCD status are read only.
				value &= 0b1111_1000;
			}
			this[address] = value;
		}
		public static bool AddressIsIO(ushort address)
		{
			return address >= IORegistersStart && address <= IORegistersEnd;
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
			bool TACEnable = (this[timerControl] & 0b00_00_01_00) != 0;
			byte TACClockSelect = (byte)(this[timerControl] & 0b00_00_00_11);
			// Each M-Cycle is 4 ticks
			int[] ticksPerInc = [256 * 4, 4 * 4, 16 * 4, 64 * 4];
			// DIV always counts up
			DIVTimer += ticks;
			int newDiv = this[divider] + DIVTimer / ticksPerInc[3];
			DIVTimer %= ticksPerInc[3];
			this[divider] = (byte)newDiv;
			// TIMA only counts up when enabled.
			if (TACEnable)
			{
				TIMATimer += ticks;
				int newCount = this[timerCounter] + TIMATimer / ticksPerInc[TACClockSelect];
				TIMATimer %= ticksPerInc[TACClockSelect];
				if (newCount > 0xff)
				{
					newCount = this[timerMod];
					// Do an interrupt.
					const ushort interruptFlag = 0xff0f;
					const int timerBit = 0b0000_0100;
					this[interruptFlag] |= timerBit;
				}
				this[timerCounter] = (byte)newCount;
			}

			PreviousCPUTick = tick;
		}
	}
}
