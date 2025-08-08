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

		const ushort DividerAddress = 0xff04;
		const ushort TimerCounterAddress = 0xff05;
		const ushort TimerModAddress = 0xff06;
		public const ushort InterruptFlagAddress = 0xff0f;
		const ushort TimerControlAddress = 0xff07;
		public const ushort OAMDMAAddress = 0xff46;
		public byte AudioTimerTick { get; private set; } = 0;

		byte[] IOMem = new byte[0x80];
		int DIVTimer = 0;
		int TIMATimer = 0;
		/// <summary>
		/// How many ticks before incrementing timer register.
		/// Each value is a possible setting.
		/// </summary>
		/// <remarks>Values are taken from https://gbdev.io/pandocs/Timer_and_Divider_Registers.html#ff07--tac-timer-control
		/// and multiplied by 4 to convert from M-cycles to T-cycles.</remarks>
		static readonly int[] TicksPerInc = [256 * 4, 4 * 4, 16 * 4, 64 * 4];
		public byte this[int i]
		{
			get => IOMem[i - IORegistersStart];
			set => IOMem[i - IORegistersStart] = value;
		}

		public IORegisters()
		{
			// initialize all registers to FF
			for (int i = 0; i < IOMem.Length; i++)
			{
				IOMem[i] = 0xff;
			}
			// https://gbdev.io/pandocs/Power_Up_Sequence.html#hardware-registers
			// Lets try setting all these to the DMG values
			this[0xFF00] = 0xCF;
			this[0xFF01] = 0x00;
			this[0xFF02] = 0x7E;
			this[0xFF04] = 0xAB;
			this[0xFF05] = 0x00;
			this[0xFF06] = 0x00;
			this[0xFF07] = 0xF8;
			this[0xFF0F] = 0xE1;
			this[0xFF10] = 0x80;
			this[0xFF11] = 0xBF;
			this[0xFF12] = 0xF3;
			this[0xFF13] = 0xFF;
			this[0xFF14] = 0xBF;
			this[0xFF16] = 0x3F;
			this[0xFF17] = 0x00;
			this[0xFF18] = 0xFF;
			this[0xFF19] = 0xBF;
			this[0xFF1A] = 0x7F;
			this[0xFF1B] = 0xFF;
			this[0xFF1C] = 0x9F;
			this[0xFF1D] = 0xFF;
			this[0xFF1E] = 0xBF;
			this[0xFF20] = 0xFF;
			this[0xFF21] = 0x00;
			this[0xFF22] = 0x00;
			this[0xFF23] = 0xBF;
			this[0xFF24] = 0x77;
			this[0xFF25] = 0xF3;
			this[0xFF26] = 0xF1;
			this[0xFF40] = 0x91;
			this[0xFF41] = 0x85;
			this[0xFF42] = 0x00;
			this[0xFF43] = 0x00;
			this[0xFF44] = 0x00;
			this[0xFF45] = 0x00;
			this[0xFF46] = 0xFF;
			this[0xFF47] = 0xFC;
			this[0xFF48] = 0x00;
			this[0xFF49] = 0x00;
			this[0xFF4A] = 0x00;
			this[0xFF4B] = 0x00;
		}

		public void CPUWrite(int address, byte value)
		{
			if (address == DividerAddress)
			{
				// https://gbdev.io/pandocs/Timer_and_Divider_Registers.html#ff04--div-divider-register
				// Writing any value to this register resets it to $00
				this[DividerAddress] = 0;
				return;
			}
			this[address] = value;
		}
		public static bool AddressIsIO(ushort address)
		{
			return address >= IORegistersStart && address <= IORegistersEnd;
		}
		public void StepTimerRegisters(ushort ticks)
		{
			// https://gbdev.io/pandocs/Timer_and_Divider_Registers.html

			bool TACEnable = (this[TimerControlAddress] & 0b00_00_01_00) != 0;
			byte TACClockSelect = (byte)(this[TimerControlAddress] & 0b00_00_00_11);
			// DIV always counts up
			DIVTimer += ticks;
			int oldDiv = this[DividerAddress];
			int newDiv = this[DividerAddress] + DIVTimer / TicksPerInc[3];
			DIVTimer %= TicksPerInc[3];
			this[DividerAddress] = (byte)newDiv;
			// Audio timer
			AudioTimerTick = 0;
			const int audioMask = 0x10;
			if ((oldDiv & audioMask) != 0 && (newDiv & audioMask) == 0)
			{
				AudioTimerTick = 1;
			}
			// TIMA only counts up when enabled.
			if (TACEnable)
			{
				TIMATimer += ticks;
				int newCount = this[TimerCounterAddress] + TIMATimer / TicksPerInc[TACClockSelect];
				TIMATimer %= TicksPerInc[TACClockSelect];
				if (newCount > 0xff)
				{
					newCount = this[TimerModAddress];
					// Do an interrupt.
					const ushort interruptFlag = 0xff0f;
					const int timerBit = 0b0000_0100;
					this[interruptFlag] |= timerBit;
				}
				this[TimerCounterAddress] = (byte)newCount;
			}
		}
	}
}
