using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Brackethouse.GB
{
	enum R8 : byte
	{
		A = 0 + 128,
		F = 0,
		B = 1 + 128,
		C = 1,
		D = 2 + 128,
		E = 2,
		H = 3 + 128,
		L = 3,
	}
	enum R16 : byte
	{
		AF = 0,
		BC = 1,
		DE = 2,
		HL = 3,
		SP = 4,
		PC = 5,
	}
	enum Flags
	{
		Zero = 1 << 7,
		Subtraction = 1 << 6,
		HalfCarry = 1 << 5,
		Carry = 1 << 4,
	}
	class CPU
	{
		delegate void Operation();
		/// <summary>
		/// Delegates for executing CPU operations.
		/// </summary>
		readonly Operation[] OpCodes = new Operation[256];
		/// <summary>
		/// Delegates for executing 0xCB prefixed CPU operations.
		/// </summary>
		readonly Operation[] CBCodes = new Operation[256];
		readonly byte[] OpCodeCycles = new byte[256];
		readonly byte[] CBCodeCycles = new byte[256];
		readonly Memory Memory;
		readonly CPURegister Registers = new();
		/// <summary>
		/// Whether interrupts can be serviced
		/// </summary>
		bool InterruptMasterEnable = false;
		/// <summary>
		/// IME delay
		/// </summary>
		bool InterruptMasterEnableQueue = false;
		bool Stopped = false;
		bool Halted = false;
		/// <summary>
		/// Counter for CPU ticks. Will rollover frequently.
		/// </summary>
		public ushort TState { get; private set; } = 0;
		/// <summary>
		/// How far to advance PC this step.
		/// </summary>
		int PCAdvance = 0;
		/// <summary>
		/// Additional ticks from conditions being met.
		/// </summary>
		ushort ConditionalTicks = 0;
		ushort LastReadWord;
		byte LastReadByte;
		readonly ushort[] InterruptTargets = [0x40, 0x48, 0x50, 0x58, 0x60];
		const ushort InterruptFlag = 0xff0f;
		const ushort InterruptEnableRegister = 0xffff;
#if DEBUG
		List<string> AddrLog = new List<string>(1000000);
#endif

		public CPU(Memory mem)
		{
			Memory = mem;
			Init();
			//Console.WriteLine($"Test register result: {TestRegisters()}");
		}

		byte Immediate8()
		{
			PCAdvance++;
			LastReadByte = Memory.Read(Registers[R16.PC] + 1);
			return LastReadByte;
		}
		ushort Immediate16()
		{
			PCAdvance += 2;
			byte byte1 = Memory.Read(Registers[R16.PC] + 1);
			byte byte2 = Memory.Read(Registers[R16.PC] + 2);
			LastReadWord = (ushort)(byte1 + (byte2 << 8));
			return LastReadWord;
		}

		bool TestRegisters()
		{
			bool result = true;
			byte[] testValues = { 9, 18, 36, 72, 108, 144, 216, 240 };
			R8[] testRegisters = { R8.A, R8.F, R8.B, R8.C, R8.D, R8.E, R8.H, R8.L };
			for (int i = 0; i < testRegisters.Length; i++)
			{
				SetR8Byte(testRegisters[i], testValues[i]);
			}
			for (int i = 0; i < testRegisters.Length; i++)
			{
				result &= GetR8Byte(testRegisters[i]) == testValues[i];
			}
			return result;
		}

		void InitOpCodes()
		{
			#region Op codes
			// x0
			OpCodes[0x00] = () => { NoOp(); };//NOP
			OpCodes[0x10] = () => { Stop(); };//STOP
			OpCodes[0x20] = () => { JumpRelativeConditional(Immediate8(), Flags.Zero, false); };// JR NZ, e8
			OpCodes[0x30] = () => { JumpRelativeConditional(Immediate8(), Flags.Carry, false); };// JR NC, e8
			OpCodes[0x40] = () => { Load(R8.B, R8.B); };
			OpCodes[0x50] = () => { Load(R8.D, R8.B); };
			OpCodes[0x60] = () => { Load(R8.H, R8.B); };
			OpCodes[0x70] = () => { Load(R16.HL, R8.B); };
			OpCodes[0x80] = () => { Add(R8.A, R8.B); };
			OpCodes[0x90] = () => { Subtract(R8.A, R8.B); };
			OpCodes[0xa0] = () => { And(R8.A, R8.B); };
			OpCodes[0xb0] = () => { Or(R8.A, R8.B); };
			OpCodes[0xc0] = () => { ReturnConditional(Flags.Zero, false); };
			OpCodes[0xd0] = () => { ReturnConditional(Flags.Carry, false); };
			OpCodes[0xe0] = () => { LoadHigh(Immediate8(), R8.A); };
			OpCodes[0xf0] = () => { LoadHigh(R8.A, Immediate8()); };
			// x1
			OpCodes[0x01] = () => { Load(R16.BC, Immediate16()); };
			OpCodes[0x11] = () => { Load(R16.DE, Immediate16()); };
			OpCodes[0x21] = () => { Load(R16.HL, Immediate16()); };
			OpCodes[0x31] = () => { Load(R16.SP, Immediate16()); };
			OpCodes[0x41] = () => { Load(R8.B, R8.B); };
			OpCodes[0x51] = () => { Load(R8.D, R8.C); };
			OpCodes[0x61] = () => { Load(R8.H, R8.C); };
			OpCodes[0x71] = () => { Load(R16.HL, R8.C); };
			OpCodes[0x81] = () => { Add(R8.A, R8.C); };
			OpCodes[0x91] = () => { Subtract(R8.A, R8.C); };
			OpCodes[0xa1] = () => { And(R8.A, R8.C); };
			OpCodes[0xb1] = () => { Or(R8.A, R8.C); };
			OpCodes[0xc1] = () => { Pop(R16.BC); };
			OpCodes[0xd1] = () => { Pop(R16.DE); };
			OpCodes[0xe1] = () => { Pop(R16.HL); };
			OpCodes[0xf1] = () => { Pop(R16.AF); };
			// x2
			OpCodes[0x02] = () => { Load(R16.BC, R8.A); };
			OpCodes[0x12] = () => { Load(R16.DE, R8.A); };
			OpCodes[0x22] = () => { LoadInc(R16.HL, R8.A); };
			OpCodes[0x32] = () => { LoadDec(R16.HL, R8.A); };
			OpCodes[0x42] = () => { Load(R8.B, R8.D); };
			OpCodes[0x52] = () => { Load(R8.D, R8.D); };
			OpCodes[0x62] = () => { Load(R8.H, R8.D); };
			OpCodes[0x72] = () => { Load(R16.HL, R8.D); };
			OpCodes[0x82] = () => { Add(R8.A, R8.D); };
			OpCodes[0x92] = () => { Subtract(R8.A, R8.D); };
			OpCodes[0xa2] = () => { And(R8.A, R8.D); };
			OpCodes[0xb2] = () => { Or(R8.A, R8.D); };
			OpCodes[0xc2] = () => { JumpConditional(Immediate16(), Flags.Zero, false); };
			OpCodes[0xd2] = () => { JumpConditional(Immediate16(), Flags.Carry, false); };
			OpCodes[0xe2] = () => { LoadHighCA(); };
			OpCodes[0xf2] = () => { LoadHighAC(); };
			// x3
			OpCodes[0x03] = () => { Increment(R16.BC); };
			OpCodes[0x13] = () => { Increment(R16.DE); };
			OpCodes[0x23] = () => { Increment(R16.HL); };
			OpCodes[0x33] = () => { Increment(R16.SP); };
			OpCodes[0x43] = () => { Load(R8.B, R8.E); };
			OpCodes[0x53] = () => { Load(R8.D, R8.E); };
			OpCodes[0x63] = () => { Load(R8.H, R8.E); };
			OpCodes[0x73] = () => { Load(R16.HL, R8.E); };
			OpCodes[0x83] = () => { Add(R8.A, R8.E); };
			OpCodes[0x93] = () => { Subtract(R8.A, R8.E); };
			OpCodes[0xa3] = () => { And(R8.A, R8.E); };
			OpCodes[0xb3] = () => { Or(R8.A, R8.E); };
			OpCodes[0xc3] = () => { Jump(Immediate16()); };
			OpCodes[0xd3] = () => { throw new Exception(); };
			OpCodes[0xe3] = () => { throw new Exception(); };
			OpCodes[0xf3] = () => { DisableInterrupt(); };
			// x4
			OpCodes[0x04] = () => { Increment(R8.B); };
			OpCodes[0x14] = () => { Increment(R8.D); };
			OpCodes[0x24] = () => { Increment(R8.H); };
			OpCodes[0x34] = () => { IncrementReference(R16.HL); };
			OpCodes[0x44] = () => { Load(R8.B, R8.H); };
			OpCodes[0x54] = () => { Load(R8.D, R8.H); };
			OpCodes[0x64] = () => { Load(R8.H, R8.H); };
			OpCodes[0x74] = () => { Load(R16.HL, R8.H); };
			OpCodes[0x84] = () => { Add(R8.A, R8.H); };
			OpCodes[0x94] = () => { Subtract(R8.A, R8.H); };
			OpCodes[0xa4] = () => { And(R8.A, R8.H); };
			OpCodes[0xb4] = () => { Or(R8.A, R8.H); };
			OpCodes[0xc4] = () => { CallConditional(Immediate16(), Flags.Zero, false); };
			OpCodes[0xd4] = () => { CallConditional(Immediate16(), Flags.Carry, false); };
			OpCodes[0xe4] = () => { throw new Exception(); };
			OpCodes[0xf4] = () => { throw new Exception(); };
			// x5
			OpCodes[0x05] = () => { Decrement(R8.B); };
			OpCodes[0x15] = () => { Decrement(R8.D); };
			OpCodes[0x25] = () => { Decrement(R8.H); };
			OpCodes[0x35] = () => { DecrementReference(R16.HL); };
			OpCodes[0x45] = () => { Load(R8.B, R8.L); };
			OpCodes[0x55] = () => { Load(R8.D, R8.L); };
			OpCodes[0x65] = () => { Load(R8.H, R8.L); };
			OpCodes[0x75] = () => { Load(R16.HL, R8.L); };
			OpCodes[0x85] = () => { Add(R8.A, R8.L); };
			OpCodes[0x95] = () => { Subtract(R8.A, R8.L); };
			OpCodes[0xa5] = () => { And(R8.A, R8.L); };
			OpCodes[0xb5] = () => { Or(R8.A, R8.L); };
			OpCodes[0xc5] = () => { Push(R16.BC); };
			OpCodes[0xd5] = () => { Push(R16.DE); };
			OpCodes[0xe5] = () => { Push(R16.HL); };
			OpCodes[0xf5] = () => { Push(R16.AF); };
			// x6
			OpCodes[0x06] = () => { Load(R8.B, Immediate8()); };
			OpCodes[0x16] = () => { Load(R8.D, Immediate8()); };
			OpCodes[0x26] = () => { Load(R8.H, Immediate8()); };
			OpCodes[0x36] = () => { Load(R16.HL, Immediate8()); };
			OpCodes[0x46] = () => { Load(R8.B, R16.HL); };
			OpCodes[0x56] = () => { Load(R8.D, R16.HL); };
			OpCodes[0x66] = () => { Load(R8.H, R16.HL); };
			OpCodes[0x76] = () => { Halt(); };
			OpCodes[0x86] = () => { Add(R8.A, R16.HL); };
			OpCodes[0x96] = () => { Subtract(R8.A, R16.HL); };
			OpCodes[0xa6] = () => { And(R8.A, R16.HL); };
			OpCodes[0xb6] = () => { Or(R8.A, R16.HL); };
			OpCodes[0xc6] = () => { Add(R8.A, Immediate8()); };
			OpCodes[0xd6] = () => { Subtract(R8.A, Immediate8()); };
			OpCodes[0xe6] = () => { And(R8.A, Immediate8()); };
			OpCodes[0xf6] = () => { Or(R8.A, Immediate8()); };
			// x7
			OpCodes[0x07] = () => { RotateLeftCarryA(R8.A); };
			OpCodes[0x17] = () => { RotateLeftA(R8.A); };
			OpCodes[0x27] = () => { DecimalAdjustAccumulator(); };
			OpCodes[0x37] = () => { SetCarryFlag(); };
			OpCodes[0x47] = () => { Load(R8.B, R8.A); };
			OpCodes[0x57] = () => { Load(R8.D, R8.A); };
			OpCodes[0x67] = () => { Load(R8.H, R8.A); };
			OpCodes[0x77] = () => { Load(R16.HL, R8.A); };
			OpCodes[0x87] = () => { Add(R8.A, R8.A); };
			OpCodes[0x97] = () => { Subtract(R8.A, R8.A); };
			OpCodes[0xa7] = () => { And(R8.A, R8.A); };
			OpCodes[0xb7] = () => { Or(R8.A, R8.A); };
			OpCodes[0xc7] = () => { Restart(0x00); };
			OpCodes[0xd7] = () => { Restart(0x10); };
			OpCodes[0xe7] = () => { Restart(0x20); };
			OpCodes[0xf7] = () => { Restart(0x30); };
			// x8
			OpCodes[0x08] = () => { Loadn16SP(Immediate16()); };
			OpCodes[0x18] = () => { JumpRelative(Immediate8()); };
			OpCodes[0x28] = () => { JumpRelativeConditional(Immediate8(), Flags.Zero, true); };
			OpCodes[0x38] = () => { JumpRelativeConditional(Immediate8(), Flags.Carry, true); };
			OpCodes[0x48] = () => { Load(R8.C, R8.B); };
			OpCodes[0x58] = () => { Load(R8.E, R8.B); };
			OpCodes[0x68] = () => { Load(R8.L, R8.B); };
			OpCodes[0x78] = () => { Load(R8.A, R8.B); };
			OpCodes[0x88] = () => { AddCarry(R8.A, R8.B); };
			OpCodes[0x98] = () => { SubtractCarry(R8.A, R8.B); };
			OpCodes[0xa8] = () => { Xor(R8.A, R8.B); };
			OpCodes[0xb8] = () => { Compare(R8.A, R8.B); };
			OpCodes[0xc8] = () => { ReturnConditional(Flags.Zero, true); };
			OpCodes[0xd8] = () => { ReturnConditional(Flags.Carry, true); };
			OpCodes[0xe8] = () => { AddSP(Immediate8()); };
			OpCodes[0xf8] = () => { LoadHLrelSP(Immediate8()); };
			// x9
			OpCodes[0x09] = () => { Add(R16.BC); };
			OpCodes[0x19] = () => { Add(R16.DE); };
			OpCodes[0x29] = () => { Add(R16.HL); };
			OpCodes[0x39] = () => { Add(R16.SP); };
			OpCodes[0x49] = () => { Load(R8.C, R8.C); };
			OpCodes[0x59] = () => { Load(R8.E, R8.C); };
			OpCodes[0x69] = () => { Load(R8.L, R8.C); };
			OpCodes[0x79] = () => { Load(R8.A, R8.C); };
			OpCodes[0x89] = () => { AddCarry(R8.A, R8.C); };
			OpCodes[0x99] = () => { SubtractCarry(R8.A, R8.C); };
			OpCodes[0xa9] = () => { Xor(R8.A, R8.C); };
			OpCodes[0xb9] = () => { Compare(R8.A, R8.C); };
			OpCodes[0xc9] = () => { Return(); };
			OpCodes[0xd9] = () => { ReturnInterrupt(); };
			OpCodes[0xe9] = () => { Jump(R16.HL); };
			OpCodes[0xf9] = () => { Load(R16.SP, R16.HL); };
			// xa
			OpCodes[0x0a] = () => { Load(R8.A, R16.BC); };
			OpCodes[0x1a] = () => { Load(R8.A, R16.DE); };
			OpCodes[0x2a] = () => { LoadInc(R8.A, R16.HL); };
			OpCodes[0x3a] = () => { LoadDec(R8.A, R16.HL); };
			OpCodes[0x4a] = () => { Load(R8.C, R8.D); };
			OpCodes[0x5a] = () => { Load(R8.E, R8.D); };
			OpCodes[0x6a] = () => { Load(R8.L, R8.D); };
			OpCodes[0x7a] = () => { Load(R8.A, R8.D); };
			OpCodes[0x8a] = () => { AddCarry(R8.A, R8.D); };
			OpCodes[0x9a] = () => { SubtractCarry(R8.A, R8.D); };
			OpCodes[0xaa] = () => { Xor(R8.A, R8.D); };
			OpCodes[0xba] = () => { Compare(R8.A, R8.D); };
			OpCodes[0xca] = () => { JumpConditional(Immediate16(), Flags.Zero, true); };
			OpCodes[0xda] = () => { JumpConditional(Immediate16(), Flags.Carry, true); };
			OpCodes[0xea] = () => { Load(Immediate16()); };
			OpCodes[0xfa] = () => { Load(R8.A, Immediate16()); };
			//xb
			OpCodes[0x0b] = () => { Decrement(R16.BC); };
			OpCodes[0x1b] = () => { Decrement(R16.DE); };
			OpCodes[0x2b] = () => { Decrement(R16.HL); };
			OpCodes[0x3b] = () => { Decrement(R16.SP); };
			OpCodes[0x4b] = () => { Load(R8.C, R8.E); };
			OpCodes[0x5b] = () => { Load(R8.E, R8.E); };
			OpCodes[0x6b] = () => { Load(R8.L, R8.E); };
			OpCodes[0x7b] = () => { Load(R8.A, R8.E); };
			OpCodes[0x8b] = () => { AddCarry(R8.A, R8.E); };
			OpCodes[0x9b] = () => { SubtractCarry(R8.A, R8.E); };
			OpCodes[0xab] = () => { Xor(R8.A, R8.E); };
			OpCodes[0xbb] = () => { Compare(R8.A, R8.E); };
			OpCodes[0xcb] = () => { CBCodes[Immediate8()].Invoke(); };
			OpCodes[0xdb] = () => { throw new Exception(); };
			OpCodes[0xeb] = () => { throw new Exception(); };
			OpCodes[0xfb] = () => { EnableInterrupt(); };
			//xc
			OpCodes[0x0c] = () => { Increment(R8.C); };
			OpCodes[0x1c] = () => { Increment(R8.E); };
			OpCodes[0x2c] = () => { Increment(R8.L); };
			OpCodes[0x3c] = () => { Increment(R8.A); };
			OpCodes[0x4c] = () => { Load(R8.C, R8.H); };
			OpCodes[0x5c] = () => { Load(R8.E, R8.H); };
			OpCodes[0x6c] = () => { Load(R8.L, R8.H); };
			OpCodes[0x7c] = () => { Load(R8.A, R8.H); };
			OpCodes[0x8c] = () => { AddCarry(R8.A, R8.H); };
			OpCodes[0x9c] = () => { SubtractCarry(R8.A, R8.H); };
			OpCodes[0xac] = () => { Xor(R8.A, R8.H); };
			OpCodes[0xbc] = () => { Compare(R8.A, R8.H); };
			OpCodes[0xcc] = () => { CallConditional(Immediate16(), Flags.Zero, true); };
			OpCodes[0xdc] = () => { CallConditional(Immediate16(), Flags.Carry, true); };
			OpCodes[0xec] = () => { throw new Exception(); };
			OpCodes[0xfc] = () => { throw new Exception(); };
			//xd
			OpCodes[0x0d] = () => { Decrement(R8.C); };
			OpCodes[0x1d] = () => { Decrement(R8.E); };
			OpCodes[0x2d] = () => { Decrement(R8.L); };
			OpCodes[0x3d] = () => { Decrement(R8.A); };
			OpCodes[0x4d] = () => { Load(R8.C, R8.L); };
			OpCodes[0x5d] = () => { Load(R8.E, R8.L); };
			OpCodes[0x6d] = () => { Load(R8.L, R8.L); };
			OpCodes[0x7d] = () => { Load(R8.A, R8.L); };
			OpCodes[0x8d] = () => { AddCarry(R8.A, R8.L); };
			OpCodes[0x9d] = () => { SubtractCarry(R8.A, R8.L); };
			OpCodes[0xad] = () => { Xor(R8.A, R8.L); };
			OpCodes[0xbd] = () => { Compare(R8.A, R8.L); };
			OpCodes[0xcd] = () => { Call(Immediate16()); };
			OpCodes[0xdd] = () => { throw new Exception(); };
			OpCodes[0xed] = () => { throw new Exception(); };
			OpCodes[0xfd] = () => { throw new Exception(); };
			//xe
			OpCodes[0x0e] = () => { Load(R8.C, Immediate8()); };
			OpCodes[0x1e] = () => { Load(R8.E, Immediate8()); };
			OpCodes[0x2e] = () => { Load(R8.L, Immediate8()); };
			OpCodes[0x3e] = () => { Load(R8.A, Immediate8()); };
			OpCodes[0x4e] = () => { Load(R8.C, R16.HL); };
			OpCodes[0x5e] = () => { Load(R8.E, R16.HL); };
			OpCodes[0x6e] = () => { Load(R8.L, R16.HL); };
			OpCodes[0x7e] = () => { Load(R8.A, R16.HL); };
			OpCodes[0x8e] = () => { AddCarry(R8.A, R16.HL); };
			OpCodes[0x9e] = () => { SubtractCarry(R8.A, R16.HL); };
			OpCodes[0xae] = () => { Xor(R8.A, R16.HL); };
			OpCodes[0xbe] = () => { Compare(R8.A, R16.HL); };
			OpCodes[0xce] = () => { AddCarry(R8.A, Immediate8()); };
			OpCodes[0xde] = () => { SubtractCarry(R8.A, Immediate8()); };
			OpCodes[0xee] = () => { Xor(R8.A, Immediate8()); };
			OpCodes[0xfe] = () => { Compare(R8.A, Immediate8()); };
			//xf
			OpCodes[0x0f] = () => { RotateRightCarryA(R8.A); };
			OpCodes[0x1f] = () => { RotateRightA(R8.A); };
			OpCodes[0x2f] = () => { Complement(R8.A); };
			OpCodes[0x3f] = () => { ComplementCarryFlag(); };
			OpCodes[0x4f] = () => { Load(R8.C, R8.A); };
			OpCodes[0x5f] = () => { Load(R8.E, R8.A); };
			OpCodes[0x6f] = () => { Load(R8.L, R8.A); };
			OpCodes[0x7f] = () => { Load(R8.A, R8.A); };
			OpCodes[0x8f] = () => { AddCarry(R8.A, R8.A); };
			OpCodes[0x9f] = () => { SubtractCarry(R8.A, R8.A); };
			OpCodes[0xaf] = () => { Xor(R8.A, R8.A); };
			OpCodes[0xbf] = () => { Compare(R8.A, R8.A); };
			OpCodes[0xcf] = () => { Restart(0x08); };
			OpCodes[0xdf] = () => { Restart(0x18); };
			OpCodes[0xef] = () => { Restart(0x28); };
			OpCodes[0xff] = () => { Restart(0x38); };
			//CBx0
			CBCodes[0x00] = () => { RotateLeftCarry(R8.B); };
			CBCodes[0x10] = () => { RotateLeft(R8.B); };
			CBCodes[0x20] = () => { ShiftLeftArithmetic(R8.B); };
			CBCodes[0x30] = () => { Swap(R8.B); };
			CBCodes[0x40] = () => { Bit(R8.B, 0b00_00_00_01); };
			CBCodes[0x50] = () => { Bit(R8.B, 0b00_00_01_00); };
			CBCodes[0x60] = () => { Bit(R8.B, 0b00_01_00_00); };
			CBCodes[0x70] = () => { Bit(R8.B, 0b01_00_00_00); };
			CBCodes[0x80] = () => { Reset(R8.B, 0b00_00_00_01); };
			CBCodes[0x90] = () => { Reset(R8.B, 0b00_00_01_00); };
			CBCodes[0xa0] = () => { Reset(R8.B, 0b00_01_00_00); };
			CBCodes[0xb0] = () => { Reset(R8.B, 0b01_00_00_00); };
			CBCodes[0xc0] = () => { Set(R8.B, 0b00_00_00_01); };
			CBCodes[0xd0] = () => { Set(R8.B, 0b00_00_01_00); };
			CBCodes[0xe0] = () => { Set(R8.B, 0b00_01_00_00); };
			CBCodes[0xf0] = () => { Set(R8.B, 0b01_00_00_00); };
			//CBx1
			CBCodes[0x01] = () => { RotateLeftCarry(R8.C); };
			CBCodes[0x11] = () => { RotateLeft(R8.C); };
			CBCodes[0x21] = () => { ShiftLeftArithmetic(R8.C); };
			CBCodes[0x31] = () => { Swap(R8.C); };
			CBCodes[0x41] = () => { Bit(R8.C, 0b00_00_00_01); };
			CBCodes[0x51] = () => { Bit(R8.C, 0b00_00_01_00); };
			CBCodes[0x61] = () => { Bit(R8.C, 0b00_01_00_00); };
			CBCodes[0x71] = () => { Bit(R8.C, 0b01_00_00_00); };
			CBCodes[0x81] = () => { Reset(R8.C, 0b00_00_00_01); };
			CBCodes[0x91] = () => { Reset(R8.C, 0b00_00_01_00); };
			CBCodes[0xa1] = () => { Reset(R8.C, 0b00_01_00_00); };
			CBCodes[0xb1] = () => { Reset(R8.C, 0b01_00_00_00); };
			CBCodes[0xc1] = () => { Set(R8.C, 0b00_00_00_01); };
			CBCodes[0xd1] = () => { Set(R8.C, 0b00_00_01_00); };
			CBCodes[0xe1] = () => { Set(R8.C, 0b00_01_00_00); };
			CBCodes[0xf1] = () => { Set(R8.C, 0b01_00_00_00); };
			//CBx2
			CBCodes[0x02] = () => { RotateLeftCarry(R8.D); };
			CBCodes[0x12] = () => { RotateLeft(R8.D); };
			CBCodes[0x22] = () => { ShiftLeftArithmetic(R8.D); };
			CBCodes[0x32] = () => { Swap(R8.D); };
			CBCodes[0x42] = () => { Bit(R8.D, 0b00_00_00_01); };
			CBCodes[0x52] = () => { Bit(R8.D, 0b00_00_01_00); };
			CBCodes[0x62] = () => { Bit(R8.D, 0b00_01_00_00); };
			CBCodes[0x72] = () => { Bit(R8.D, 0b01_00_00_00); };
			CBCodes[0x82] = () => { Reset(R8.D, 0b00_00_00_01); };
			CBCodes[0x92] = () => { Reset(R8.D, 0b00_00_01_00); };
			CBCodes[0xa2] = () => { Reset(R8.D, 0b00_01_00_00); };
			CBCodes[0xb2] = () => { Reset(R8.D, 0b01_00_00_00); };
			CBCodes[0xc2] = () => { Set(R8.D, 0b00_00_00_01); };
			CBCodes[0xd2] = () => { Set(R8.D, 0b00_00_01_00); };
			CBCodes[0xe2] = () => { Set(R8.D, 0b00_01_00_00); };
			CBCodes[0xf2] = () => { Set(R8.D, 0b01_00_00_00); };
			//CBx3
			CBCodes[0x03] = () => { RotateLeftCarry(R8.E); };
			CBCodes[0x13] = () => { RotateLeft(R8.E); };
			CBCodes[0x23] = () => { ShiftLeftArithmetic(R8.E); };
			CBCodes[0x33] = () => { Swap(R8.E); };
			CBCodes[0x43] = () => { Bit(R8.E, 0b00_00_00_01); };
			CBCodes[0x53] = () => { Bit(R8.E, 0b00_00_01_00); };
			CBCodes[0x63] = () => { Bit(R8.E, 0b00_01_00_00); };
			CBCodes[0x73] = () => { Bit(R8.E, 0b01_00_00_00); };
			CBCodes[0x83] = () => { Reset(R8.E, 0b00_00_00_01); };
			CBCodes[0x93] = () => { Reset(R8.E, 0b00_00_01_00); };
			CBCodes[0xa3] = () => { Reset(R8.E, 0b00_01_00_00); };
			CBCodes[0xb3] = () => { Reset(R8.E, 0b01_00_00_00); };
			CBCodes[0xc3] = () => { Set(R8.E, 0b00_00_00_01); };
			CBCodes[0xd3] = () => { Set(R8.E, 0b00_00_01_00); };
			CBCodes[0xe3] = () => { Set(R8.E, 0b00_01_00_00); };
			CBCodes[0xf3] = () => { Set(R8.E, 0b01_00_00_00); };
			//CBx4
			CBCodes[0x04] = () => { RotateLeftCarry(R8.H); };
			CBCodes[0x14] = () => { RotateLeft(R8.H); };
			CBCodes[0x24] = () => { ShiftLeftArithmetic(R8.H); };
			CBCodes[0x34] = () => { Swap(R8.H); };
			CBCodes[0x44] = () => { Bit(R8.H, 0b00_00_00_01); };
			CBCodes[0x54] = () => { Bit(R8.H, 0b00_00_01_00); };
			CBCodes[0x64] = () => { Bit(R8.H, 0b00_01_00_00); };
			CBCodes[0x74] = () => { Bit(R8.H, 0b01_00_00_00); };
			CBCodes[0x84] = () => { Reset(R8.H, 0b00_00_00_01); };
			CBCodes[0x94] = () => { Reset(R8.H, 0b00_00_01_00); };
			CBCodes[0xa4] = () => { Reset(R8.H, 0b00_01_00_00); };
			CBCodes[0xb4] = () => { Reset(R8.H, 0b01_00_00_00); };
			CBCodes[0xc4] = () => { Set(R8.H, 0b00_00_00_01); };
			CBCodes[0xd4] = () => { Set(R8.H, 0b00_00_01_00); };
			CBCodes[0xe4] = () => { Set(R8.H, 0b00_01_00_00); };
			CBCodes[0xf4] = () => { Set(R8.H, 0b01_00_00_00); };
			//CBx5
			CBCodes[0x05] = () => { RotateLeftCarry(R8.L); };
			CBCodes[0x15] = () => { RotateLeft(R8.L); };
			CBCodes[0x25] = () => { ShiftLeftArithmetic(R8.L); };
			CBCodes[0x35] = () => { Swap(R8.L); };
			CBCodes[0x45] = () => { Bit(R8.L, 0b00_00_00_01); };
			CBCodes[0x55] = () => { Bit(R8.L, 0b00_00_01_00); };
			CBCodes[0x65] = () => { Bit(R8.L, 0b00_01_00_00); };
			CBCodes[0x75] = () => { Bit(R8.L, 0b01_00_00_00); };
			CBCodes[0x85] = () => { Reset(R8.L, 0b00_00_00_01); };
			CBCodes[0x95] = () => { Reset(R8.L, 0b00_00_01_00); };
			CBCodes[0xa5] = () => { Reset(R8.L, 0b00_01_00_00); };
			CBCodes[0xb5] = () => { Reset(R8.L, 0b01_00_00_00); };
			CBCodes[0xc5] = () => { Set(R8.L, 0b00_00_00_01); };
			CBCodes[0xd5] = () => { Set(R8.L, 0b00_00_01_00); };
			CBCodes[0xe5] = () => { Set(R8.L, 0b00_01_00_00); };
			CBCodes[0xf5] = () => { Set(R8.L, 0b01_00_00_00); };
			//CBx6
			CBCodes[0x06] = () => { RotateLeftCarry(R16.HL); };
			CBCodes[0x16] = () => { RotateLeft(R16.HL); };
			CBCodes[0x26] = () => { ShiftLeftArithmetic(R16.HL); };
			CBCodes[0x36] = () => { Swap(R16.HL); };
			CBCodes[0x46] = () => { Bit(R16.HL, 0b00_00_00_01); };
			CBCodes[0x56] = () => { Bit(R16.HL, 0b00_00_01_00); };
			CBCodes[0x66] = () => { Bit(R16.HL, 0b00_01_00_00); };
			CBCodes[0x76] = () => { Bit(R16.HL, 0b01_00_00_00); };
			CBCodes[0x86] = () => { Reset(R16.HL, 0b00_00_00_01); };
			CBCodes[0x96] = () => { Reset(R16.HL, 0b00_00_01_00); };
			CBCodes[0xa6] = () => { Reset(R16.HL, 0b00_01_00_00); };
			CBCodes[0xb6] = () => { Reset(R16.HL, 0b01_00_00_00); };
			CBCodes[0xc6] = () => { Set(R16.HL, 0b00_00_00_01); };
			CBCodes[0xd6] = () => { Set(R16.HL, 0b00_00_01_00); };
			CBCodes[0xe6] = () => { Set(R16.HL, 0b00_01_00_00); };
			CBCodes[0xf6] = () => { Set(R16.HL, 0b01_00_00_00); };
			//CBx7
			CBCodes[0x07] = () => { RotateLeftCarry(R8.A); };
			CBCodes[0x17] = () => { RotateLeft(R8.A); };
			CBCodes[0x27] = () => { ShiftLeftArithmetic(R8.A); };
			CBCodes[0x37] = () => { Swap(R8.A); };
			CBCodes[0x47] = () => { Bit(R8.A, 0b00_00_00_01); };
			CBCodes[0x57] = () => { Bit(R8.A, 0b00_00_01_00); };
			CBCodes[0x67] = () => { Bit(R8.A, 0b00_01_00_00); };
			CBCodes[0x77] = () => { Bit(R8.A, 0b01_00_00_00); };
			CBCodes[0x87] = () => { Reset(R8.A, 0b00_00_00_01); };
			CBCodes[0x97] = () => { Reset(R8.A, 0b00_00_01_00); };
			CBCodes[0xa7] = () => { Reset(R8.A, 0b00_01_00_00); };
			CBCodes[0xb7] = () => { Reset(R8.A, 0b01_00_00_00); };
			CBCodes[0xc7] = () => { Set(R8.A, 0b00_00_00_01); };
			CBCodes[0xd7] = () => { Set(R8.A, 0b00_00_01_00); };
			CBCodes[0xe7] = () => { Set(R8.A, 0b00_01_00_00); };
			CBCodes[0xf7] = () => { Set(R8.A, 0b01_00_00_00); };
			//CBx8
			CBCodes[0x08] = () => { RotateRightCarry(R8.B); };
			CBCodes[0x18] = () => { RotateRight(R8.B); };
			CBCodes[0x28] = () => { ShiftRightArithmetic(R8.B); };
			CBCodes[0x38] = () => { ShiftRightLogic(R8.B); };
			CBCodes[0x48] = () => { Bit(R8.B, 0b00_00_00_10); };
			CBCodes[0x58] = () => { Bit(R8.B, 0b00_00_10_00); };
			CBCodes[0x68] = () => { Bit(R8.B, 0b00_10_00_00); };
			CBCodes[0x78] = () => { Bit(R8.B, 0b10_00_00_00); };
			CBCodes[0x88] = () => { Reset(R8.B, 0b00_00_00_10); };
			CBCodes[0x98] = () => { Reset(R8.B, 0b00_00_10_00); };
			CBCodes[0xa8] = () => { Reset(R8.B, 0b00_10_00_00); };
			CBCodes[0xb8] = () => { Reset(R8.B, 0b10_00_00_00); };
			CBCodes[0xc8] = () => { Set(R8.B, 0b00_00_00_10); };
			CBCodes[0xd8] = () => { Set(R8.B, 0b00_00_10_00); };
			CBCodes[0xe8] = () => { Set(R8.B, 0b00_10_00_00); };
			CBCodes[0xf8] = () => { Set(R8.B, 0b10_00_00_00); };
			//CBx9
			CBCodes[0x09] = () => { RotateRightCarry(R8.C); };
			CBCodes[0x19] = () => { RotateRight(R8.C); };
			CBCodes[0x29] = () => { ShiftRightArithmetic(R8.C); };
			CBCodes[0x39] = () => { ShiftRightLogic(R8.C); };
			CBCodes[0x49] = () => { Bit(R8.C, 0b00_00_00_10); };
			CBCodes[0x59] = () => { Bit(R8.C, 0b00_00_10_00); };
			CBCodes[0x69] = () => { Bit(R8.C, 0b00_10_00_00); };
			CBCodes[0x79] = () => { Bit(R8.C, 0b10_00_00_00); };
			CBCodes[0x89] = () => { Reset(R8.C, 0b00_00_00_10); };
			CBCodes[0x99] = () => { Reset(R8.C, 0b00_00_10_00); };
			CBCodes[0xa9] = () => { Reset(R8.C, 0b00_10_00_00); };
			CBCodes[0xb9] = () => { Reset(R8.C, 0b10_00_00_00); };
			CBCodes[0xc9] = () => { Set(R8.C, 0b00_00_00_10); };
			CBCodes[0xd9] = () => { Set(R8.C, 0b00_00_10_00); };
			CBCodes[0xe9] = () => { Set(R8.C, 0b00_10_00_00); };
			CBCodes[0xf9] = () => { Set(R8.C, 0b10_00_00_00); };
			//CBxa
			CBCodes[0x0a] = () => { RotateRightCarry(R8.D); };
			CBCodes[0x1a] = () => { RotateRight(R8.D); };
			CBCodes[0x2a] = () => { ShiftRightArithmetic(R8.D); };
			CBCodes[0x3a] = () => { ShiftRightLogic(R8.D); };
			CBCodes[0x4a] = () => { Bit(R8.D, 0b00_00_00_10); };
			CBCodes[0x5a] = () => { Bit(R8.D, 0b00_00_10_00); };
			CBCodes[0x6a] = () => { Bit(R8.D, 0b00_10_00_00); };
			CBCodes[0x7a] = () => { Bit(R8.D, 0b10_00_00_00); };
			CBCodes[0x8a] = () => { Reset(R8.D, 0b00_00_00_10); };
			CBCodes[0x9a] = () => { Reset(R8.D, 0b00_00_10_00); };
			CBCodes[0xaa] = () => { Reset(R8.D, 0b00_10_00_00); };
			CBCodes[0xba] = () => { Reset(R8.D, 0b10_00_00_00); };
			CBCodes[0xca] = () => { Set(R8.D, 0b00_00_00_10); };
			CBCodes[0xda] = () => { Set(R8.D, 0b00_00_10_00); };
			CBCodes[0xea] = () => { Set(R8.D, 0b00_10_00_00); };
			CBCodes[0xfa] = () => { Set(R8.D, 0b10_00_00_00); };
			//CBxb
			CBCodes[0x0b] = () => { RotateRightCarry(R8.E); };
			CBCodes[0x1b] = () => { RotateRight(R8.E); };
			CBCodes[0x2b] = () => { ShiftRightArithmetic(R8.E); };
			CBCodes[0x3b] = () => { ShiftRightLogic(R8.E); };
			CBCodes[0x4b] = () => { Bit(R8.E, 0b00_00_00_10); };
			CBCodes[0x5b] = () => { Bit(R8.E, 0b00_00_10_00); };
			CBCodes[0x6b] = () => { Bit(R8.E, 0b00_10_00_00); };
			CBCodes[0x7b] = () => { Bit(R8.E, 0b10_00_00_00); };
			CBCodes[0x8b] = () => { Reset(R8.E, 0b00_00_00_10); };
			CBCodes[0x9b] = () => { Reset(R8.E, 0b00_00_10_00); };
			CBCodes[0xab] = () => { Reset(R8.E, 0b00_10_00_00); };
			CBCodes[0xbb] = () => { Reset(R8.E, 0b10_00_00_00); };
			CBCodes[0xcb] = () => { Set(R8.E, 0b00_00_00_10); };
			CBCodes[0xdb] = () => { Set(R8.E, 0b00_00_10_00); };
			CBCodes[0xeb] = () => { Set(R8.E, 0b00_10_00_00); };
			CBCodes[0xfb] = () => { Set(R8.E, 0b10_00_00_00); };
			//CBxc
			CBCodes[0x0c] = () => { RotateRightCarry(R8.H); };
			CBCodes[0x1c] = () => { RotateRight(R8.H); };
			CBCodes[0x2c] = () => { ShiftRightArithmetic(R8.H); };
			CBCodes[0x3c] = () => { ShiftRightLogic(R8.H); };
			CBCodes[0x4c] = () => { Bit(R8.H, 0b00_00_00_10); };
			CBCodes[0x5c] = () => { Bit(R8.H, 0b00_00_10_00); };
			CBCodes[0x6c] = () => { Bit(R8.H, 0b00_10_00_00); };
			CBCodes[0x7c] = () => { Bit(R8.H, 0b10_00_00_00); };
			CBCodes[0x8c] = () => { Reset(R8.H, 0b00_00_00_10); };
			CBCodes[0x9c] = () => { Reset(R8.H, 0b00_00_10_00); };
			CBCodes[0xac] = () => { Reset(R8.H, 0b00_10_00_00); };
			CBCodes[0xbc] = () => { Reset(R8.H, 0b10_00_00_00); };
			CBCodes[0xcc] = () => { Set(R8.H, 0b00_00_00_10); };
			CBCodes[0xdc] = () => { Set(R8.H, 0b00_00_10_00); };
			CBCodes[0xec] = () => { Set(R8.H, 0b00_10_00_00); };
			CBCodes[0xfc] = () => { Set(R8.H, 0b10_00_00_00); };
			//CBxd
			CBCodes[0x0d] = () => { RotateRightCarry(R8.L); };
			CBCodes[0x1d] = () => { RotateRight(R8.L); };
			CBCodes[0x2d] = () => { ShiftRightArithmetic(R8.L); };
			CBCodes[0x3d] = () => { ShiftRightLogic(R8.L); };
			CBCodes[0x4d] = () => { Bit(R8.L, 0b00_00_00_10); };
			CBCodes[0x5d] = () => { Bit(R8.L, 0b00_00_10_00); };
			CBCodes[0x6d] = () => { Bit(R8.L, 0b00_10_00_00); };
			CBCodes[0x7d] = () => { Bit(R8.L, 0b10_00_00_00); };
			CBCodes[0x8d] = () => { Reset(R8.L, 0b00_00_00_10); };
			CBCodes[0x9d] = () => { Reset(R8.L, 0b00_00_10_00); };
			CBCodes[0xad] = () => { Reset(R8.L, 0b00_10_00_00); };
			CBCodes[0xbd] = () => { Reset(R8.L, 0b10_00_00_00); };
			CBCodes[0xcd] = () => { Set(R8.L, 0b00_00_00_10); };
			CBCodes[0xdd] = () => { Set(R8.L, 0b00_00_10_00); };
			CBCodes[0xed] = () => { Set(R8.L, 0b00_10_00_00); };
			CBCodes[0xfd] = () => { Set(R8.L, 0b10_00_00_00); };
			//CBxe
			CBCodes[0x0e] = () => { RotateRightCarry(R16.HL); };
			CBCodes[0x1e] = () => { RotateRight(R16.HL); };
			CBCodes[0x2e] = () => { ShiftRightArithmetic(R16.HL); };
			CBCodes[0x3e] = () => { ShiftRightLogic(R16.HL); };
			CBCodes[0x4e] = () => { Bit(R16.HL, 0b00_00_00_10); };
			CBCodes[0x5e] = () => { Bit(R16.HL, 0b00_00_10_00); };
			CBCodes[0x6e] = () => { Bit(R16.HL, 0b00_10_00_00); };
			CBCodes[0x7e] = () => { Bit(R16.HL, 0b10_00_00_00); };
			CBCodes[0x8e] = () => { Reset(R16.HL, 0b00_00_00_10); };
			CBCodes[0x9e] = () => { Reset(R16.HL, 0b00_00_10_00); };
			CBCodes[0xae] = () => { Reset(R16.HL, 0b00_10_00_00); };
			CBCodes[0xbe] = () => { Reset(R16.HL, 0b10_00_00_00); };
			CBCodes[0xce] = () => { Set(R16.HL, 0b00_00_00_10); };
			CBCodes[0xde] = () => { Set(R16.HL, 0b00_00_10_00); };
			CBCodes[0xee] = () => { Set(R16.HL, 0b00_10_00_00); };
			CBCodes[0xfe] = () => { Set(R16.HL, 0b10_00_00_00); };
			//CBxf
			CBCodes[0x0f] = () => { RotateRightCarry(R8.A); };
			CBCodes[0x1f] = () => { RotateRight(R8.A); };
			CBCodes[0x2f] = () => { ShiftRightArithmetic(R8.A); };
			CBCodes[0x3f] = () => { ShiftRightLogic(R8.A); };
			CBCodes[0x4f] = () => { Bit(R8.A, 0b00_00_00_10); };
			CBCodes[0x5f] = () => { Bit(R8.A, 0b00_00_10_00); };
			CBCodes[0x6f] = () => { Bit(R8.A, 0b00_10_00_00); };
			CBCodes[0x7f] = () => { Bit(R8.A, 0b10_00_00_00); };
			CBCodes[0x8f] = () => { Reset(R8.A, 0b00_00_00_10); };
			CBCodes[0x9f] = () => { Reset(R8.A, 0b00_00_10_00); };
			CBCodes[0xaf] = () => { Reset(R8.A, 0b00_10_00_00); };
			CBCodes[0xbf] = () => { Reset(R8.A, 0b10_00_00_00); };
			CBCodes[0xcf] = () => { Set(R8.A, 0b00_00_00_10); };
			CBCodes[0xdf] = () => { Set(R8.A, 0b00_00_10_00); };
			CBCodes[0xef] = () => { Set(R8.A, 0b00_10_00_00); };
			CBCodes[0xff] = () => { Set(R8.A, 0b10_00_00_00); };
			#endregion
			// Cycletimes
			#region Cycle times
			OpCodeCycles[0x00] = 4;
			OpCodeCycles[0x01] = 12;
			OpCodeCycles[0x02] = 8;
			OpCodeCycles[0x03] = 8;
			OpCodeCycles[0x04] = 4;
			OpCodeCycles[0x05] = 4;
			OpCodeCycles[0x06] = 8;
			OpCodeCycles[0x07] = 4;
			OpCodeCycles[0x08] = 20;
			OpCodeCycles[0x09] = 8;
			OpCodeCycles[0x0A] = 8;
			OpCodeCycles[0x0B] = 8;
			OpCodeCycles[0x0C] = 4;
			OpCodeCycles[0x0D] = 4;
			OpCodeCycles[0x0E] = 8;
			OpCodeCycles[0x0F] = 4;
			OpCodeCycles[0x10] = 4;
			OpCodeCycles[0x11] = 12;
			OpCodeCycles[0x12] = 8;
			OpCodeCycles[0x13] = 8;
			OpCodeCycles[0x14] = 4;
			OpCodeCycles[0x15] = 4;
			OpCodeCycles[0x16] = 8;
			OpCodeCycles[0x17] = 4;
			OpCodeCycles[0x18] = 12;
			OpCodeCycles[0x19] = 8;
			OpCodeCycles[0x1A] = 8;
			OpCodeCycles[0x1B] = 8;
			OpCodeCycles[0x1C] = 4;
			OpCodeCycles[0x1D] = 4;
			OpCodeCycles[0x1E] = 8;
			OpCodeCycles[0x1F] = 4;
			OpCodeCycles[0x20] = 8;
			OpCodeCycles[0x21] = 12;
			OpCodeCycles[0x22] = 8;
			OpCodeCycles[0x23] = 8;
			OpCodeCycles[0x24] = 4;
			OpCodeCycles[0x25] = 4;
			OpCodeCycles[0x26] = 8;
			OpCodeCycles[0x27] = 4;
			OpCodeCycles[0x28] = 8;
			OpCodeCycles[0x29] = 8;
			OpCodeCycles[0x2A] = 8;
			OpCodeCycles[0x2B] = 8;
			OpCodeCycles[0x2C] = 4;
			OpCodeCycles[0x2D] = 4;
			OpCodeCycles[0x2E] = 8;
			OpCodeCycles[0x2F] = 4;
			OpCodeCycles[0x30] = 8;
			OpCodeCycles[0x31] = 12;
			OpCodeCycles[0x32] = 8;
			OpCodeCycles[0x33] = 8;
			OpCodeCycles[0x34] = 12;
			OpCodeCycles[0x35] = 12;
			OpCodeCycles[0x36] = 12;
			OpCodeCycles[0x37] = 4;
			OpCodeCycles[0x38] = 8;
			OpCodeCycles[0x39] = 8;
			OpCodeCycles[0x3A] = 8;
			OpCodeCycles[0x3B] = 8;
			OpCodeCycles[0x3C] = 4;
			OpCodeCycles[0x3D] = 4;
			OpCodeCycles[0x3E] = 8;
			OpCodeCycles[0x3F] = 4;
			OpCodeCycles[0x40] = 4;
			OpCodeCycles[0x41] = 4;
			OpCodeCycles[0x42] = 4;
			OpCodeCycles[0x43] = 4;
			OpCodeCycles[0x44] = 4;
			OpCodeCycles[0x45] = 4;
			OpCodeCycles[0x46] = 8;
			OpCodeCycles[0x47] = 4;
			OpCodeCycles[0x48] = 4;
			OpCodeCycles[0x49] = 4;
			OpCodeCycles[0x4A] = 4;
			OpCodeCycles[0x4B] = 4;
			OpCodeCycles[0x4C] = 4;
			OpCodeCycles[0x4D] = 4;
			OpCodeCycles[0x4E] = 8;
			OpCodeCycles[0x4F] = 4;
			OpCodeCycles[0x50] = 4;
			OpCodeCycles[0x51] = 4;
			OpCodeCycles[0x52] = 4;
			OpCodeCycles[0x53] = 4;
			OpCodeCycles[0x54] = 4;
			OpCodeCycles[0x55] = 4;
			OpCodeCycles[0x56] = 8;
			OpCodeCycles[0x57] = 4;
			OpCodeCycles[0x58] = 4;
			OpCodeCycles[0x59] = 4;
			OpCodeCycles[0x5A] = 4;
			OpCodeCycles[0x5B] = 4;
			OpCodeCycles[0x5C] = 4;
			OpCodeCycles[0x5D] = 4;
			OpCodeCycles[0x5E] = 8;
			OpCodeCycles[0x5F] = 4;
			OpCodeCycles[0x60] = 4;
			OpCodeCycles[0x61] = 4;
			OpCodeCycles[0x62] = 4;
			OpCodeCycles[0x63] = 4;
			OpCodeCycles[0x64] = 4;
			OpCodeCycles[0x65] = 4;
			OpCodeCycles[0x66] = 8;
			OpCodeCycles[0x67] = 4;
			OpCodeCycles[0x68] = 4;
			OpCodeCycles[0x69] = 4;
			OpCodeCycles[0x6A] = 4;
			OpCodeCycles[0x6B] = 4;
			OpCodeCycles[0x6C] = 4;
			OpCodeCycles[0x6D] = 4;
			OpCodeCycles[0x6E] = 8;
			OpCodeCycles[0x6F] = 4;
			OpCodeCycles[0x70] = 8;
			OpCodeCycles[0x71] = 8;
			OpCodeCycles[0x72] = 8;
			OpCodeCycles[0x73] = 8;
			OpCodeCycles[0x74] = 8;
			OpCodeCycles[0x75] = 8;
			OpCodeCycles[0x76] = 4;
			OpCodeCycles[0x77] = 8;
			OpCodeCycles[0x78] = 4;
			OpCodeCycles[0x79] = 4;
			OpCodeCycles[0x7A] = 4;
			OpCodeCycles[0x7B] = 4;
			OpCodeCycles[0x7C] = 4;
			OpCodeCycles[0x7D] = 4;
			OpCodeCycles[0x7E] = 8;
			OpCodeCycles[0x7F] = 4;
			OpCodeCycles[0x80] = 4;
			OpCodeCycles[0x81] = 4;
			OpCodeCycles[0x82] = 4;
			OpCodeCycles[0x83] = 4;
			OpCodeCycles[0x84] = 4;
			OpCodeCycles[0x85] = 4;
			OpCodeCycles[0x86] = 8;
			OpCodeCycles[0x87] = 4;
			OpCodeCycles[0x88] = 4;
			OpCodeCycles[0x89] = 4;
			OpCodeCycles[0x8A] = 4;
			OpCodeCycles[0x8B] = 4;
			OpCodeCycles[0x8C] = 4;
			OpCodeCycles[0x8D] = 4;
			OpCodeCycles[0x8E] = 8;
			OpCodeCycles[0x8F] = 4;
			OpCodeCycles[0x90] = 4;
			OpCodeCycles[0x91] = 4;
			OpCodeCycles[0x92] = 4;
			OpCodeCycles[0x93] = 4;
			OpCodeCycles[0x94] = 4;
			OpCodeCycles[0x95] = 4;
			OpCodeCycles[0x96] = 8;
			OpCodeCycles[0x97] = 4;
			OpCodeCycles[0x98] = 4;
			OpCodeCycles[0x99] = 4;
			OpCodeCycles[0x9A] = 4;
			OpCodeCycles[0x9B] = 4;
			OpCodeCycles[0x9C] = 4;
			OpCodeCycles[0x9D] = 4;
			OpCodeCycles[0x9E] = 8;
			OpCodeCycles[0x9F] = 4;
			OpCodeCycles[0xA0] = 4;
			OpCodeCycles[0xA1] = 4;
			OpCodeCycles[0xA2] = 4;
			OpCodeCycles[0xA3] = 4;
			OpCodeCycles[0xA4] = 4;
			OpCodeCycles[0xA5] = 4;
			OpCodeCycles[0xA6] = 8;
			OpCodeCycles[0xA7] = 4;
			OpCodeCycles[0xA8] = 4;
			OpCodeCycles[0xA9] = 4;
			OpCodeCycles[0xAA] = 4;
			OpCodeCycles[0xAB] = 4;
			OpCodeCycles[0xAC] = 4;
			OpCodeCycles[0xAD] = 4;
			OpCodeCycles[0xAE] = 8;
			OpCodeCycles[0xAF] = 4;
			OpCodeCycles[0xB0] = 4;
			OpCodeCycles[0xB1] = 4;
			OpCodeCycles[0xB2] = 4;
			OpCodeCycles[0xB3] = 4;
			OpCodeCycles[0xB4] = 4;
			OpCodeCycles[0xB5] = 4;
			OpCodeCycles[0xB6] = 8;
			OpCodeCycles[0xB7] = 4;
			OpCodeCycles[0xB8] = 4;
			OpCodeCycles[0xB9] = 4;
			OpCodeCycles[0xBA] = 4;
			OpCodeCycles[0xBB] = 4;
			OpCodeCycles[0xBC] = 4;
			OpCodeCycles[0xBD] = 4;
			OpCodeCycles[0xBE] = 8;
			OpCodeCycles[0xBF] = 4;
			OpCodeCycles[0xC0] = 8;
			OpCodeCycles[0xC1] = 12;
			OpCodeCycles[0xC2] = 12;
			OpCodeCycles[0xC3] = 16;
			OpCodeCycles[0xC4] = 12;
			OpCodeCycles[0xC5] = 16;
			OpCodeCycles[0xC6] = 8;
			OpCodeCycles[0xC7] = 16;
			OpCodeCycles[0xC8] = 8;
			OpCodeCycles[0xC9] = 16;
			OpCodeCycles[0xCA] = 12;
			OpCodeCycles[0xCB] = 4;
			OpCodeCycles[0xCC] = 12;
			OpCodeCycles[0xCD] = 24;
			OpCodeCycles[0xCE] = 8;
			OpCodeCycles[0xCF] = 16;
			OpCodeCycles[0xD0] = 8;
			OpCodeCycles[0xD1] = 12;
			OpCodeCycles[0xD2] = 12;
			OpCodeCycles[0xD3] = 4;
			OpCodeCycles[0xD4] = 12;
			OpCodeCycles[0xD5] = 16;
			OpCodeCycles[0xD6] = 8;
			OpCodeCycles[0xD7] = 16;
			OpCodeCycles[0xD8] = 8;
			OpCodeCycles[0xD9] = 16;
			OpCodeCycles[0xDA] = 12;
			OpCodeCycles[0xDB] = 4;
			OpCodeCycles[0xDC] = 12;
			OpCodeCycles[0xDD] = 4;
			OpCodeCycles[0xDE] = 8;
			OpCodeCycles[0xDF] = 16;
			OpCodeCycles[0xE0] = 12;
			OpCodeCycles[0xE1] = 12;
			OpCodeCycles[0xE2] = 8;
			OpCodeCycles[0xE3] = 4;
			OpCodeCycles[0xE4] = 4;
			OpCodeCycles[0xE5] = 16;
			OpCodeCycles[0xE6] = 8;
			OpCodeCycles[0xE7] = 16;
			OpCodeCycles[0xE8] = 16;
			OpCodeCycles[0xE9] = 4;
			OpCodeCycles[0xEA] = 16;
			OpCodeCycles[0xEB] = 4;
			OpCodeCycles[0xEC] = 4;
			OpCodeCycles[0xED] = 4;
			OpCodeCycles[0xEE] = 8;
			OpCodeCycles[0xEF] = 16;
			OpCodeCycles[0xF0] = 12;
			OpCodeCycles[0xF1] = 12;
			OpCodeCycles[0xF2] = 8;
			OpCodeCycles[0xF3] = 4;
			OpCodeCycles[0xF4] = 4;
			OpCodeCycles[0xF5] = 16;
			OpCodeCycles[0xF6] = 8;
			OpCodeCycles[0xF7] = 16;
			OpCodeCycles[0xF8] = 12;
			OpCodeCycles[0xF9] = 8;
			OpCodeCycles[0xFA] = 16;
			OpCodeCycles[0xFB] = 4;
			OpCodeCycles[0xFC] = 4;
			OpCodeCycles[0xFD] = 4;
			OpCodeCycles[0xFE] = 8;
			OpCodeCycles[0xFF] = 16;
			CBCodeCycles[0x00] = 8;
			CBCodeCycles[0x01] = 8;
			CBCodeCycles[0x02] = 8;
			CBCodeCycles[0x03] = 8;
			CBCodeCycles[0x04] = 8;
			CBCodeCycles[0x05] = 8;
			CBCodeCycles[0x06] = 16;
			CBCodeCycles[0x07] = 8;
			CBCodeCycles[0x08] = 8;
			CBCodeCycles[0x09] = 8;
			CBCodeCycles[0x0A] = 8;
			CBCodeCycles[0x0B] = 8;
			CBCodeCycles[0x0C] = 8;
			CBCodeCycles[0x0D] = 8;
			CBCodeCycles[0x0E] = 16;
			CBCodeCycles[0x0F] = 8;
			CBCodeCycles[0x10] = 8;
			CBCodeCycles[0x11] = 8;
			CBCodeCycles[0x12] = 8;
			CBCodeCycles[0x13] = 8;
			CBCodeCycles[0x14] = 8;
			CBCodeCycles[0x15] = 8;
			CBCodeCycles[0x16] = 16;
			CBCodeCycles[0x17] = 8;
			CBCodeCycles[0x18] = 8;
			CBCodeCycles[0x19] = 8;
			CBCodeCycles[0x1A] = 8;
			CBCodeCycles[0x1B] = 8;
			CBCodeCycles[0x1C] = 8;
			CBCodeCycles[0x1D] = 8;
			CBCodeCycles[0x1E] = 16;
			CBCodeCycles[0x1F] = 8;
			CBCodeCycles[0x20] = 8;
			CBCodeCycles[0x21] = 8;
			CBCodeCycles[0x22] = 8;
			CBCodeCycles[0x23] = 8;
			CBCodeCycles[0x24] = 8;
			CBCodeCycles[0x25] = 8;
			CBCodeCycles[0x26] = 16;
			CBCodeCycles[0x27] = 8;
			CBCodeCycles[0x28] = 8;
			CBCodeCycles[0x29] = 8;
			CBCodeCycles[0x2A] = 8;
			CBCodeCycles[0x2B] = 8;
			CBCodeCycles[0x2C] = 8;
			CBCodeCycles[0x2D] = 8;
			CBCodeCycles[0x2E] = 16;
			CBCodeCycles[0x2F] = 8;
			CBCodeCycles[0x30] = 8;
			CBCodeCycles[0x31] = 8;
			CBCodeCycles[0x32] = 8;
			CBCodeCycles[0x33] = 8;
			CBCodeCycles[0x34] = 8;
			CBCodeCycles[0x35] = 8;
			CBCodeCycles[0x36] = 16;
			CBCodeCycles[0x37] = 8;
			CBCodeCycles[0x38] = 8;
			CBCodeCycles[0x39] = 8;
			CBCodeCycles[0x3A] = 8;
			CBCodeCycles[0x3B] = 8;
			CBCodeCycles[0x3C] = 8;
			CBCodeCycles[0x3D] = 8;
			CBCodeCycles[0x3E] = 16;
			CBCodeCycles[0x3F] = 8;
			CBCodeCycles[0x40] = 8;
			CBCodeCycles[0x41] = 8;
			CBCodeCycles[0x42] = 8;
			CBCodeCycles[0x43] = 8;
			CBCodeCycles[0x44] = 8;
			CBCodeCycles[0x45] = 8;
			CBCodeCycles[0x46] = 12;
			CBCodeCycles[0x47] = 8;
			CBCodeCycles[0x48] = 8;
			CBCodeCycles[0x49] = 8;
			CBCodeCycles[0x4A] = 8;
			CBCodeCycles[0x4B] = 8;
			CBCodeCycles[0x4C] = 8;
			CBCodeCycles[0x4D] = 8;
			CBCodeCycles[0x4E] = 12;
			CBCodeCycles[0x4F] = 8;
			CBCodeCycles[0x50] = 8;
			CBCodeCycles[0x51] = 8;
			CBCodeCycles[0x52] = 8;
			CBCodeCycles[0x53] = 8;
			CBCodeCycles[0x54] = 8;
			CBCodeCycles[0x55] = 8;
			CBCodeCycles[0x56] = 12;
			CBCodeCycles[0x57] = 8;
			CBCodeCycles[0x58] = 8;
			CBCodeCycles[0x59] = 8;
			CBCodeCycles[0x5A] = 8;
			CBCodeCycles[0x5B] = 8;
			CBCodeCycles[0x5C] = 8;
			CBCodeCycles[0x5D] = 8;
			CBCodeCycles[0x5E] = 12;
			CBCodeCycles[0x5F] = 8;
			CBCodeCycles[0x60] = 8;
			CBCodeCycles[0x61] = 8;
			CBCodeCycles[0x62] = 8;
			CBCodeCycles[0x63] = 8;
			CBCodeCycles[0x64] = 8;
			CBCodeCycles[0x65] = 8;
			CBCodeCycles[0x66] = 12;
			CBCodeCycles[0x67] = 8;
			CBCodeCycles[0x68] = 8;
			CBCodeCycles[0x69] = 8;
			CBCodeCycles[0x6A] = 8;
			CBCodeCycles[0x6B] = 8;
			CBCodeCycles[0x6C] = 8;
			CBCodeCycles[0x6D] = 8;
			CBCodeCycles[0x6E] = 12;
			CBCodeCycles[0x6F] = 8;
			CBCodeCycles[0x70] = 8;
			CBCodeCycles[0x71] = 8;
			CBCodeCycles[0x72] = 8;
			CBCodeCycles[0x73] = 8;
			CBCodeCycles[0x74] = 8;
			CBCodeCycles[0x75] = 8;
			CBCodeCycles[0x76] = 12;
			CBCodeCycles[0x77] = 8;
			CBCodeCycles[0x78] = 8;
			CBCodeCycles[0x79] = 8;
			CBCodeCycles[0x7A] = 8;
			CBCodeCycles[0x7B] = 8;
			CBCodeCycles[0x7C] = 8;
			CBCodeCycles[0x7D] = 8;
			CBCodeCycles[0x7E] = 12;
			CBCodeCycles[0x7F] = 8;
			CBCodeCycles[0x80] = 8;
			CBCodeCycles[0x81] = 8;
			CBCodeCycles[0x82] = 8;
			CBCodeCycles[0x83] = 8;
			CBCodeCycles[0x84] = 8;
			CBCodeCycles[0x85] = 8;
			CBCodeCycles[0x86] = 16;
			CBCodeCycles[0x87] = 8;
			CBCodeCycles[0x88] = 8;
			CBCodeCycles[0x89] = 8;
			CBCodeCycles[0x8A] = 8;
			CBCodeCycles[0x8B] = 8;
			CBCodeCycles[0x8C] = 8;
			CBCodeCycles[0x8D] = 8;
			CBCodeCycles[0x8E] = 16;
			CBCodeCycles[0x8F] = 8;
			CBCodeCycles[0x90] = 8;
			CBCodeCycles[0x91] = 8;
			CBCodeCycles[0x92] = 8;
			CBCodeCycles[0x93] = 8;
			CBCodeCycles[0x94] = 8;
			CBCodeCycles[0x95] = 8;
			CBCodeCycles[0x96] = 16;
			CBCodeCycles[0x97] = 8;
			CBCodeCycles[0x98] = 8;
			CBCodeCycles[0x99] = 8;
			CBCodeCycles[0x9A] = 8;
			CBCodeCycles[0x9B] = 8;
			CBCodeCycles[0x9C] = 8;
			CBCodeCycles[0x9D] = 8;
			CBCodeCycles[0x9E] = 16;
			CBCodeCycles[0x9F] = 8;
			CBCodeCycles[0xA0] = 8;
			CBCodeCycles[0xA1] = 8;
			CBCodeCycles[0xA2] = 8;
			CBCodeCycles[0xA3] = 8;
			CBCodeCycles[0xA4] = 8;
			CBCodeCycles[0xA5] = 8;
			CBCodeCycles[0xA6] = 16;
			CBCodeCycles[0xA7] = 8;
			CBCodeCycles[0xA8] = 8;
			CBCodeCycles[0xA9] = 8;
			CBCodeCycles[0xAA] = 8;
			CBCodeCycles[0xAB] = 8;
			CBCodeCycles[0xAC] = 8;
			CBCodeCycles[0xAD] = 8;
			CBCodeCycles[0xAE] = 16;
			CBCodeCycles[0xAF] = 8;
			CBCodeCycles[0xB0] = 8;
			CBCodeCycles[0xB1] = 8;
			CBCodeCycles[0xB2] = 8;
			CBCodeCycles[0xB3] = 8;
			CBCodeCycles[0xB4] = 8;
			CBCodeCycles[0xB5] = 8;
			CBCodeCycles[0xB6] = 16;
			CBCodeCycles[0xB7] = 8;
			CBCodeCycles[0xB8] = 8;
			CBCodeCycles[0xB9] = 8;
			CBCodeCycles[0xBA] = 8;
			CBCodeCycles[0xBB] = 8;
			CBCodeCycles[0xBC] = 8;
			CBCodeCycles[0xBD] = 8;
			CBCodeCycles[0xBE] = 16;
			CBCodeCycles[0xBF] = 8;
			CBCodeCycles[0xC0] = 8;
			CBCodeCycles[0xC1] = 8;
			CBCodeCycles[0xC2] = 8;
			CBCodeCycles[0xC3] = 8;
			CBCodeCycles[0xC4] = 8;
			CBCodeCycles[0xC5] = 8;
			CBCodeCycles[0xC6] = 16;
			CBCodeCycles[0xC7] = 8;
			CBCodeCycles[0xC8] = 8;
			CBCodeCycles[0xC9] = 8;
			CBCodeCycles[0xCA] = 8;
			CBCodeCycles[0xCB] = 8;
			CBCodeCycles[0xCC] = 8;
			CBCodeCycles[0xCD] = 8;
			CBCodeCycles[0xCE] = 16;
			CBCodeCycles[0xCF] = 8;
			CBCodeCycles[0xD0] = 8;
			CBCodeCycles[0xD1] = 8;
			CBCodeCycles[0xD2] = 8;
			CBCodeCycles[0xD3] = 8;
			CBCodeCycles[0xD4] = 8;
			CBCodeCycles[0xD5] = 8;
			CBCodeCycles[0xD6] = 16;
			CBCodeCycles[0xD7] = 8;
			CBCodeCycles[0xD8] = 8;
			CBCodeCycles[0xD9] = 8;
			CBCodeCycles[0xDA] = 8;
			CBCodeCycles[0xDB] = 8;
			CBCodeCycles[0xDC] = 8;
			CBCodeCycles[0xDD] = 8;
			CBCodeCycles[0xDE] = 16;
			CBCodeCycles[0xDF] = 8;
			CBCodeCycles[0xE0] = 8;
			CBCodeCycles[0xE1] = 8;
			CBCodeCycles[0xE2] = 8;
			CBCodeCycles[0xE3] = 8;
			CBCodeCycles[0xE4] = 8;
			CBCodeCycles[0xE5] = 8;
			CBCodeCycles[0xE6] = 16;
			CBCodeCycles[0xE7] = 8;
			CBCodeCycles[0xE8] = 8;
			CBCodeCycles[0xE9] = 8;
			CBCodeCycles[0xEA] = 8;
			CBCodeCycles[0xEB] = 8;
			CBCodeCycles[0xEC] = 8;
			CBCodeCycles[0xED] = 8;
			CBCodeCycles[0xEE] = 16;
			CBCodeCycles[0xEF] = 8;
			CBCodeCycles[0xF0] = 8;
			CBCodeCycles[0xF1] = 8;
			CBCodeCycles[0xF2] = 8;
			CBCodeCycles[0xF3] = 8;
			CBCodeCycles[0xF4] = 8;
			CBCodeCycles[0xF5] = 8;
			CBCodeCycles[0xF6] = 16;
			CBCodeCycles[0xF7] = 8;
			CBCodeCycles[0xF8] = 8;
			CBCodeCycles[0xF9] = 8;
			CBCodeCycles[0xFA] = 8;
			CBCodeCycles[0xFB] = 8;
			CBCodeCycles[0xFC] = 8;
			CBCodeCycles[0xFD] = 8;
			CBCodeCycles[0xFE] = 16;
			CBCodeCycles[0xFF] = 8;
			#endregion
		}

		#region Manipulating registers and flags
		/// <summary>
		/// Get a byte value from the 16-bit registers
		/// </summary>
		/// <param name="register">Which 8-bit register to access</param>
		/// <returns>The value that was stored.</returns>
		byte GetR8Byte(R8 register)
		{
			return Registers[register];
		}
		void SetR8Byte(R8 register, byte value)
		{
			Registers[register] = value;
		}
		/// <summary>
		/// Get value of a flag
		/// </summary>
		/// <param name="flag">Flag to get.</param>
		/// <returns>Whether flag was set.</returns>
		bool GetFlag(Flags flag)
		{
			return Registers[flag];
		}
		/// <summary>
		/// Set a flag to a value.
		/// </summary>
		/// <param name="flag">Flag to set.</param>
		/// <param name="value">Value to set flag to.</param>
		void SetFlag(Flags flag, bool value)
		{
			Registers[flag] = value;
			}
		#endregion
		/// <summary>
		/// Initialize OpCodes and registers
		/// </summary>
		void Init()
		{
			InitOpCodes();
			// https://gbdev.io/pandocs/Power_Up_Sequence.html#cpu-registers
			// Lets try DMG values
			const ushort startingPoint = 0x0100;
			Registers[R16.AF] = 0x01b0;
			Registers[R16.BC] = 0x0013;
			Registers[R16.DE] = 0x00d8;
			Registers[R16.HL] = 0x014d;
			Registers[R16.PC] = startingPoint;
			Registers[R16.SP] = 0xfffe;
		}

		public void Step()
		{
			byte interruptByte = Memory[InterruptEnableRegister];
			interruptByte &= Memory[InterruptFlag];
			if (interruptByte != 0)
			{
				Halted = false;
				if (InterruptMasterEnable)
				{
				// Do interrupt here.
				for (int i = 0; i < InterruptTargets.Length; i++)
				{
					byte checkBit = 1;
					checkBit <<= i;
					if ((checkBit & interruptByte) == checkBit)
					{
						InterruptMasterEnable = false;
						Memory[InterruptFlag] ^= checkBit;

						Push(R16.PC);
						Jump(InterruptTargets[i]);
						TState += 20;
						return;
					}
				}
			}
			}
			if (Halted || Stopped)
			{
				TState += 4;
				return;
			}
			InterruptMasterEnable |= InterruptMasterEnableQueue;
			InterruptMasterEnableQueue = false;
			PCAdvance = 1;
			ConditionalTicks = 0;
			ushort address = Registers[R16.PC];
			byte op = Memory.Read(address);
#if DEBUG
			AddrLog.Add(address.ToString("X4"));
			if (AddrLog.Count == 100000)
			{
				File.WriteAllLines("addrlog.txt", AddrLog);
			}
			if (address == 0x0040)
			{

			}
			if (address == 0x27cc)
			{

			}
#endif
			OpCodes[op].Invoke();
			Registers[R16.PC] += (ushort)Math.Clamp(PCAdvance,0, 3);
			// It's not entirely clear if the CB cycle is in addition to the CB op
			// I think it is.
			TState += OpCodeCycles[op];
			TState += ConditionalTicks;
			if (op == 0xCB)
			{
				TState += CBCodeCycles[LastReadByte];
			}
		}
		// Instructions implemented according to this reference:
		// https://rgbds.gbdev.io/docs/v0.9.1/gbz80.7
		#region Load Instructions
		/// <summary>
		/// <c>LD r8,r8</c><br />
		/// Copy (aka Load) the value in register on the right into the register on the left.
		/// </summary>
		/// <param name="dest">Register</param>
		/// <param name="src">Register</param>
		void Load(R8 dest, R8 src)
		{
			SetR8Byte(dest, GetR8Byte(src));
		}
		/// <summary>
		/// <c>LD r8,n8</c><br />
		/// Copy the value n8 into register r8.
		/// </summary>
		/// <param name="dest">r8</param>
		/// <param name="value">n8</param>
		void Load(R8 dest, byte value)
		{
			SetR8Byte(dest, value);
		}
		/// <summary>
		/// <c>LD r16,n16</c><br />
		/// Copy the value n16 into register r16.
		/// </summary>
		/// <param name="dest">r16</param>
		/// <param name="value">n16</param>
		void Load(R16 dest, ushort value)
		{
			Registers[dest] = value;
		}
		/// <summary>
		/// <c>LD [HL],r8</c><br />
		/// Copy the value in register r8 into the byte pointed to by HL.
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		void Load(R16 a, R8 b)
		{
			// a might always be HL
			Memory.Write(Registers[a], GetR8Byte(b));
		}
		/// <summary>
		/// <c>LD [HL],n8</c><br />
		/// Copy the value n8 into the byte pointed to by HL.
		/// </summary>
		/// <param name="a"></param>
		/// <param name="value"></param>
		void Load(R16 a, byte value)
		{
			// a might always be HL
			Memory.Write(Registers[a], value);
		}
		/// <summary>
		/// <c>LD r8,[HL]</c><br />
		/// Copy the value pointed to by HL into register r8.
		/// </summary>
		/// <param name="dest">HL</param>
		/// <param name="src">r8</param>
		/// <remarks>Also used for <c>LD A,[r16]</c></remarks>
		void Load(R8 dest, R16 src)
		{
			// src might always be HL
			SetR8Byte(dest, Memory.Read(Registers[src]));
		}
		/// <summary>
		/// <c>LD [r16],A</c><br />
		/// Copy the value in register A into the byte pointed to by r16.
		/// </summary>
		/// <param name="dest">r16</param>
		void Load(R16 dest)
		{
			Memory.Write(Registers[dest], GetR8Byte(R8.A));
		}
		/// <summary>
		/// <c>LD [n16],A</c><br />
		/// Copy the value in register A into the byte at address n16.
		/// </summary>
		/// <param name="dest"></param>
		void Load(ushort dest)
		{
			Memory.Write(dest, GetR8Byte(R8.A));
		}
		/// <summary>
		/// <c>LDH [n16],A</c><br />
		/// Copy the value in register A into the byte at address n16, provided the address is between $FF00 and $FFFF.
		/// </summary>
		/// <param name="dest"></param>
		void LoadHigh(byte dest, R8 src)
		{
			Memory.Write(0xff00 + dest, GetR8Byte(src));
		}
		/// <summary>
		/// <c>LDH [C],A</c><br />
		/// Copy the value in register A into the byte at address $FF00+C.
		/// </summary>
		void LoadHighCA()
		{
			Memory.Write(0xff00 + GetR8Byte(R8.C), GetR8Byte(R8.A));
		}
		/// <summary>
		/// <c>LD A,[n16]</c><br />
		/// Copy the byte at address n16 into register A.
		/// </summary>
		/// <param name="dest">Destination register. Maybe always A in actual use.</param>
		/// <param name="srcAddr">Address to copy from</param>
		void Load(R8 dest, ushort srcAddr)
		{
			SetR8Byte(dest, Memory.Read(srcAddr));
		}
		void LoadHigh(R8 dest, byte src)
		{
			SetR8Byte(dest, Memory.Read(0xff00 + src));
		}
		/// <summary>
		/// <c>LDH A,[C]</c><br />
		/// Copy the byte at address $FF00+C into register A.
		/// </summary>
		void LoadHighAC()
		{
			SetR8Byte(R8.A, Memory.Read(0xff00 + GetR8Byte(R8.C)));
		}
		/// <summary>
		/// <c>LD [HLI],A</c><br />
		/// Copy the value in register A into the byte pointed by HL and increment HL afterwards.
		/// </summary>
		/// <param name="dest">Destination register address. Increment after.</param>
		/// <param name="src">Source register.</param>
		void LoadInc(R16 dest, R8 src)
		{
			Memory.Write(Registers[dest], GetR8Byte(src));
			Registers[dest]++;
		}
		/// <summary>
		/// <c>LD [HLD],A</c><br />
		/// Copy the value in register A into the byte pointed by HL and decrement HL afterwards.
		/// </summary>
		/// <param name="dest"></param>
		/// <param name="src"></param>
		void LoadDec(R16 dest, R8 src)
		{
			Memory.Write(Registers[dest], GetR8Byte(src));
			Registers[dest]--;
		}
		/// <summary>
		/// <c>LD A,[HLI]</c><br/>
		/// Copy the byte pointed to by HL into register A, and increment HL afterwards.
		/// </summary>
		/// <param name="dest"></param>
		/// <param name="src"></param>
		void LoadInc(R8 dest, R16 src)
		{
			SetR8Byte(dest, Memory.Read(Registers[src]));
			Registers[src]++;
		}
		/// <summary>
		/// <c>LD A,[HLD]</c><br/>
		/// Copy the byte pointed to by HL into register A, and decrement HL afterwards.
		/// </summary>
		/// <param name="dest"></param>
		/// <param name="src"></param>
		void LoadDec(R8 dest, R16 src)
		{
			SetR8Byte(dest, Memory.Read(Registers[src]));
			Registers[src]--;
		}

		#endregion
		#region 8-bit arithmetic instructions
		/// <summary>
		/// <c>ADC A,r8</c><br/>
		/// Add the value in r8 plus the carry flag to A.
		/// </summary>
		/// <param name="dest"></param>
		/// <param name="src"></param>
		void AddCarry(R8 dest, R8 src)
		{
			AddCarry(dest, GetR8Byte(src));
		}
		/// <summary>
		/// <c>ADC A,[HL]</c><br/>
		/// Add the byte pointed to by HL plus the carry flag to A.
		/// </summary>
		/// <param name="dest"></param>
		/// <param name="srcAddr"></param>
		void AddCarry(R8 dest, R16 srcAddr)
		{
			AddCarry(dest, Memory.Read(Registers[srcAddr]));
		}
		/// <summary>
		/// <c>ADC A,n8</c><br/>
		/// Add the value n8 plus the carry flag to A.
		/// </summary>
		/// <param name="dest"></param>
		/// <param name="value"></param>
		void AddCarry(R8 dest, byte value)
		{
			// Looks like dest should always be R8.A
			int oldVal = GetR8Byte(dest);
			byte carryB = Convert.ToByte(GetFlag(Flags.Carry));
			int intResult = oldVal + value + carryB;
			byte byteResult = (byte)intResult;
			SetFlag(Flags.Zero, byteResult == 0);
			SetFlag(Flags.Subtraction, false);
			SetFlag(Flags.HalfCarry, ((oldVal & 0x0f) + (value & 0x0f) + carryB) >= 0x10);
			SetFlag(Flags.Carry, intResult > 0xff);
			SetR8Byte(dest, byteResult);
		}
		/// <summary>
		/// <c>ADD A,r8</c><br />
		/// Add the value in r8 to A.
		/// </summary>
		/// <param name="dest"></param>
		/// <param name="src"></param>
		void Add(R8 dest, R8 src)
		{
			Add(dest, GetR8Byte(src));
		}
		/// <summary>
		/// <c>ADD A,[HL]</c><br />
		/// Add the byte pointed to by HL to A.
		/// </summary>
		/// <param name="dest"></param>
		/// <param name="srcAddr"></param>
		void Add(R8 dest, R16 srcAddr)
		{
			Add(dest, Memory.Read(Registers[srcAddr]));
		}
		/// <summary>
		/// <c>ADD A,n8</c><br />
		/// Add the value n8 to A.
		/// </summary>
		/// <param name="dest"></param>
		/// <param name="value"></param>
		void Add(R8 dest, byte value)
		{
			// Looks like dest should always be R8.A
			int oldVal = GetR8Byte(dest);
			int intResult = oldVal + value;
			byte byteResult = (byte)intResult;
			SetFlag(Flags.Zero, byteResult == 0);
			SetFlag(Flags.Subtraction, false);
			SetFlag(Flags.HalfCarry, ((oldVal & 0x0f) + (value & 0x0f)) >= 0x10);
			SetFlag(Flags.Carry, intResult > 0xff);
			SetR8Byte(dest, byteResult);
		}
		/// <summary>
		/// <c>CP A,r8</c><br/>
		/// ComPare the value in A with the value in r8.
		/// </summary>
		/// <param name="regA"></param>
		/// <param name="regB"></param>
		/// <remarks>This subtracts the value in r8 from A and sets flags accordingly, but discards the result.</remarks>
		void Compare(R8 regA, R8 regB)
		{
			Compare(regA, GetR8Byte(regB));
		}
		/// <summary>
		/// <c>CP A,[HL]</c><br/>
		/// ComPare the value in A with the byte pointed to by HL.
		/// </summary>
		/// <param name="reg"></param>
		/// <param name="addr"></param>
		/// <remarks>This subtracts the byte pointed to by HL from A and sets flags accordingly, but discards the result.</remarks>
		void Compare(R8 reg, R16 addr)
		{
			Compare(reg, Memory.Read(Registers[addr]));
		}
		/// <summary>
		/// <c>CP A,n8</c><br/>
		/// ComPare the value in A with the value n8.
		/// </summary>
		/// <param name="reg"></param>
		/// <param name="value"></param>
		/// <remarks>This subtracts the value n8 from A and sets flags accordingly, but discards the result.</remarks>
		void Compare(R8 reg, byte value)
		{
			byte regVal = GetR8Byte(reg);
			SetFlag(Flags.Zero, regVal == value);
			SetFlag(Flags.Subtraction, true);
			SetFlag(Flags.HalfCarry, (regVal & 0x0f) < (value & 0x0f));
			SetFlag(Flags.Carry, value > regVal);
		}
		void Decrement(R8 reg)
		{
			byte val = GetR8Byte(reg);
			bool halfcarry = (val & 0x0f) == 0x00;
			val--;
			SetR8Byte(reg, val);
			SetFlag(Flags.Zero, val == 0);
			SetFlag(Flags.Subtraction, true);
			SetFlag(Flags.HalfCarry,halfcarry);
		}
		/// <summary>
		/// Decrement the value pointed at by the register.
		/// Used for <c>DEC [HL]</c>
		/// </summary>
		/// <param name="addr">An address</param>
		void DecrementReference(R16 addr)
		{
			byte val = Memory.Read(Registers[addr]);
			bool halfcarry = (val & 0x0f) == 0x00;
			val--;
			Memory.Write(Registers[addr], val);
			SetFlag(Flags.Zero, val == 0);
			SetFlag(Flags.Subtraction, true);
			SetFlag(Flags.HalfCarry, halfcarry);
		}
		void Increment(R8 reg)
		{
			byte val = GetR8Byte(reg);
			bool halfcarry = (val & 0x0f) == 0x0f;
			val++;
			SetR8Byte(reg, val);
			SetFlag(Flags.Zero, val == 0);
			SetFlag(Flags.Subtraction, false);
			SetFlag(Flags.HalfCarry, halfcarry);
		}
		/// <summary>
		/// Increment the value pointed at by the register.
		/// Used for <c>INC [HL]</c>
		/// </summary>
		/// <param name="addr">An address</param>
		void IncrementReference(R16 addr)
		{
			byte val = Memory.Read(Registers[addr]);
			bool halfcarry = (val & 0x0f) == 0x0f;
			val++;
			Memory.Write(Registers[addr], val);
			SetFlag(Flags.Zero, val == 0);
			SetFlag(Flags.Subtraction, false);
			SetFlag(Flags.HalfCarry, halfcarry);
		}
		void SubtractCarry(R8 regA, R8 regB)
		{
			SubtractCarry(regA, GetR8Byte(regB));
		}
		void SubtractCarry(R8 regA, R16 regB)
		{
			SubtractCarry(regA, Memory.Read(Registers[regB]));
		}
		void SubtractCarry(R8 reg, byte value)
		{
			// Looks like reg should always be R8.A
			int oldVal = GetR8Byte(reg);
			byte carryB = Convert.ToByte(GetFlag(Flags.Carry));
			int intResult = oldVal - value - carryB;
			byte byteResult = (byte)intResult;
			bool halfCarry = (oldVal & 0x0f) - (value & 0x0f) - carryB < 0;

			SetFlag(Flags.Zero, byteResult == 0);
			SetFlag(Flags.Subtraction, true);
			SetFlag(Flags.HalfCarry, halfCarry);
			SetFlag(Flags.Carry, (value + carryB) > oldVal);
			SetR8Byte(reg, byteResult);
		}
		void Subtract(R8 regA, R8 regB)
		{
			Subtract(regA, GetR8Byte(regB));
		}
		void Subtract(R8 regA, R16 regB)
		{
			Subtract(regA, Memory.Read(Registers[regB]));
		}
		void Subtract(R8 reg, byte value)
		{
			// Looks like reg should always be R8.A
			int oldVal = GetR8Byte(reg);
			int intResult = oldVal - value;
			byte byteResult = (byte)intResult;

			SetFlag(Flags.Zero, byteResult == 0);
			SetFlag(Flags.Subtraction, true);
			SetFlag(Flags.HalfCarry, (oldVal & 0x0f) < (value & 0x0f));
			SetFlag(Flags.Carry, value > oldVal);
			SetR8Byte(reg, byteResult);
		}
		#endregion
		#region 16-bit arithmetic instructions
		void Add(R16 reg)
		{
			int oldVal = Registers[R16.HL];
			int addVal = Registers[reg];
			int newVal = oldVal + addVal;
			SetFlag(Flags.Subtraction, false);
			SetFlag(Flags.HalfCarry, (oldVal & 0x0fff) + (addVal & 0x0fff) > 0x0fff);
			SetFlag(Flags.Carry, newVal > 0xffff);
			Registers[R16.HL] = (ushort)newVal;
		}
		void Increment(R16 reg)
		{
			Registers[reg]++;
		}
		void Decrement(R16 reg)
		{
			Registers[reg]--;
		}
		#endregion
		#region Bitwise logic instructions
		void And(R8 reg, byte value)
		{
			SetR8Byte(reg, (byte)(GetR8Byte(reg) & value));
			SetFlag(Flags.Zero, GetR8Byte(reg) == 0);
			SetFlag(Flags.Subtraction, false);
			SetFlag(Flags.HalfCarry, true);
			SetFlag(Flags.Carry, false);
		}
		void And(R8 regA, R8 regB)
		{
			And(regA, GetR8Byte(regB));
		}
		void And(R8 regA, R16 regB)
		{
			And(regA, Memory.Read(Registers[regB]));
		}
		void Complement(R8 reg)
		{
			SetR8Byte(reg, (byte)(~GetR8Byte(reg)));
			SetFlag(Flags.Subtraction, true);
			SetFlag(Flags.HalfCarry, true);
		}
		void Or(R8 reg, byte value)
		{
			SetR8Byte(reg, (byte)(GetR8Byte(reg) | value));
			SetFlag(Flags.Zero, GetR8Byte(reg) == 0);
			SetFlag(Flags.Subtraction, false);
			SetFlag(Flags.HalfCarry, false);
			SetFlag(Flags.Carry, false);
		}
		void Or(R8 regA, R8 regB)
		{
			Or(regA, GetR8Byte(regB));
		}
		void Or(R8 regA, R16 regB)
		{
			Or(regA, Memory.Read(Registers[regB]));
		}

		void Xor(R8 reg, byte value)
		{
			SetR8Byte(reg, (byte)(GetR8Byte(reg) ^ value));
			SetFlag(Flags.Zero, GetR8Byte(reg) == 0);
			SetFlag(Flags.Subtraction, false);
			SetFlag(Flags.HalfCarry, false);
			SetFlag(Flags.Carry, false);
		}
		void Xor(R8 regA, R8 regB)
		{
			Xor(regA, GetR8Byte(regB));
		}
		void Xor(R8 regA, R16 regB)
		{
			Xor(regA, Memory.Read(Registers[regB]));
		}

		#endregion
		#region Bit flag instructions
		void Bit(R8 reg, byte bitmask)
		{
			bool zero = (GetR8Byte(reg) & bitmask) == 0;
			SetFlag(Flags.Zero, zero);
			SetFlag(Flags.Subtraction, false);
			SetFlag(Flags.HalfCarry, true);
		}
		void Bit(R16 address, byte bitmask)
		{
			bool zero = (Memory.Read(Registers[address]) & bitmask) == 0;
			SetFlag(Flags.Zero, zero);
			SetFlag(Flags.Subtraction, false);
			SetFlag(Flags.HalfCarry, true);
		}
		void Reset(R8 reg, byte bitmask)
		{
			byte bitReset = (byte)(GetR8Byte(reg) & (~bitmask));
			SetR8Byte(reg, bitReset);
		}
		void Reset(R16 address, byte bitmask)
		{
			byte bitReset = (byte)(Memory.Read(Registers[address]) & (~bitmask));
			Memory.Write(Registers[address], bitReset);
		}
		void Set(R8 reg, byte bitmask)
		{
			SetR8Byte(reg, (byte)(GetR8Byte(reg) | bitmask));
		}
		void Set(R16 address, byte bitmask)
		{
			byte bitSet = (byte)(Memory.Read(Registers[address]) | bitmask);
			Memory.Write(Registers[address], bitSet);
		}
		#endregion
		#region Bit shift instructions
		/// <summary>
		/// Rotate bits in register r8 left, through the carry flag.
		/// </summary>
		/// <param name="reg">Register r8</param>
		void RotateLeft(R8 reg)
		{
			byte oldVal = GetR8Byte(reg);
			int newVal = oldVal << 1;
			newVal += Convert.ToByte(GetFlag(Flags.Carry));
			SetFlag(Flags.Zero, newVal == 0);
			SetFlag(Flags.Subtraction, false);
			SetFlag(Flags.HalfCarry, false);
			const int carryMask = 0x0100;
			SetFlag(Flags.Carry, (newVal & carryMask) != 0);
			SetR8Byte(reg, (byte)newVal);
		}
		void RotateLeft(R16 address)
		{
			byte oldVal = Memory.Read(Registers[address]);
			int newVal = oldVal << 1;
			newVal += Convert.ToByte(GetFlag(Flags.Carry));
			SetFlag(Flags.Zero, newVal == 0);
			SetFlag(Flags.Subtraction, false);
			SetFlag(Flags.HalfCarry, false);
			const int carryMask = 0x0100;
			SetFlag(Flags.Carry, (newVal & carryMask) != 0);
			Memory.Write(Registers[address], (byte)newVal);
		}
		/// <summary>
		/// Does <see cref="RotateLeft(R8)"/> then sets Zero flag to 0.
		/// </summary>
		/// <param name="reg"></param>
		void RotateLeftA(R8 reg)
		{
			RotateLeft(reg);
			SetFlag(Flags.Zero, false);
		}
		/// <summary>
		/// RLC r8
		/// </summary>
		/// <param name="reg"></param>
		void RotateLeftCarry(R8 reg)
		{
			byte oldVal = GetR8Byte(reg);
			byte rotated = (byte)((oldVal << 1) | (oldVal >>> (8 - 1)));
			SetFlag(Flags.Zero, rotated == 0);
			SetFlag(Flags.Subtraction, false);
			SetFlag(Flags.HalfCarry, false);
			const int carryMask = 0x80;
			SetFlag(Flags.Carry, (oldVal & carryMask) != 0);
			SetR8Byte(reg, rotated);
		}
		void RotateLeftCarry(R16 address)
		{
			byte oldVal = Memory.Read(Registers[address]);
			byte rotated = (byte)((oldVal << 1) | (oldVal >>> (8 - 1)));
			SetFlag(Flags.Zero, rotated == 0);
			SetFlag(Flags.Subtraction, false);
			SetFlag(Flags.HalfCarry, false);
			const int carryMask = 0x80;
			SetFlag(Flags.Carry, (oldVal & carryMask) != 0);
			Memory.Write(Registers[address], rotated);
		}
		void RotateLeftCarryA(R8 reg)
		{
			RotateLeftCarry(reg);
			SetFlag(Flags.Zero, false);
		}
		void RotateRight(R8 reg)
		{
			byte oldVal = GetR8Byte(reg);
			int newVal = oldVal >>> 1;
			newVal += Convert.ToByte(GetFlag(Flags.Carry)) << 7;
			byte newByte = (byte)newVal;
			SetFlag(Flags.Zero, newByte == 0);
			SetFlag(Flags.Subtraction, false);
			SetFlag(Flags.HalfCarry, false);
			const int carryMask = 0x01;
			SetFlag(Flags.Carry, (oldVal & carryMask) != 0);
			SetR8Byte(reg, newByte);
		}
		void RotateRight(R16 address)
		{
			byte oldVal = Memory.Read(Registers[address]);
			int newVal = oldVal >>> 1;
			newVal += Convert.ToByte(GetFlag(Flags.Carry)) << 7;
			byte newByte = (byte)newVal;
			SetFlag(Flags.Zero, newByte == 0);
			SetFlag(Flags.Subtraction, false);
			SetFlag(Flags.HalfCarry, false);
			const int carryMask = 0x01;
			SetFlag(Flags.Carry, (oldVal & carryMask) != 0);
			Memory.Write(Registers[address], newByte);
		}
		void RotateRightA(R8 reg)
		{
			RotateRight(reg);
			SetFlag(Flags.Zero, false);
		}
		void RotateRightCarry(R8 reg)
		{
			byte oldVal = GetR8Byte(reg);
			byte rotated = (byte)((oldVal >>> 1) | (oldVal << (8 - 1)));
			SetFlag(Flags.Zero, rotated == 0);
			SetFlag(Flags.Subtraction, false);
			SetFlag(Flags.HalfCarry, false);
			const int carryMask = 0x0001;
			SetFlag(Flags.Carry, (oldVal & carryMask) != 0);
			SetR8Byte(reg, rotated);
		}
		void RotateRightCarry(R16 address)
		{
			byte oldVal = Memory.Read(Registers[address]);
			byte rotated = (byte)((oldVal >>> 1) | (oldVal << (8 - 1)));
			SetFlag(Flags.Zero, rotated == 0);
			SetFlag(Flags.Subtraction, false);
			SetFlag(Flags.HalfCarry, false);
			const int carryMask = 0x0001;
			SetFlag(Flags.Carry, (oldVal & carryMask) != 0);
			Memory.Write(Registers[address], rotated);
		}
		void RotateRightCarryA(R8 reg)
		{
			RotateRightCarry(reg);
			SetFlag(Flags.Zero, false);
			// TODO: Double check flags
		}
		void ShiftLeftArithmetic(R8 reg)
		{
			byte oldVal = GetR8Byte(reg);
			int newVal = oldVal << 1;
			byte newByte = (byte)newVal;
			SetFlag(Flags.Zero, newByte == 0);
			SetFlag(Flags.Subtraction, false);
			SetFlag(Flags.HalfCarry, false);
			const int carryMask = 0x80;
			SetFlag(Flags.Carry, (oldVal & carryMask) != 0);
			SetR8Byte(reg, newByte);
		}
		void ShiftLeftArithmetic(R16 address)
		{
			byte oldVal = Memory.Read(Registers[address]);
			int newVal = oldVal << 1;
			byte newByte = (byte)newVal;
			SetFlag(Flags.Zero, newByte == 0);
			SetFlag(Flags.Subtraction, false);
			SetFlag(Flags.HalfCarry, false);
			const int carryMask = 0x80;
			SetFlag(Flags.Carry, (oldVal & carryMask) != 0);
			Memory.Write(Registers[address], newByte);
		}
		void ShiftRightArithmetic(R8 reg)
		{
			byte oldVal = GetR8Byte(reg);
			int newVal = oldVal >> 1;
			byte newByte = (byte)newVal;
			SetFlag(Flags.Zero, newByte == 0);
			SetFlag(Flags.Subtraction, false);
			SetFlag(Flags.HalfCarry, false);
			const int carryMask = 0x01;
			SetFlag(Flags.Carry, (oldVal & carryMask) != 0);
			SetR8Byte(reg, newByte);
		}
		void ShiftRightArithmetic(R16 address)
		{
			byte oldVal = Memory.Read(Registers[address]);
			int newVal = oldVal >> 1;
			byte newByte = (byte)newVal;
			SetFlag(Flags.Zero, newByte == 0);
			SetFlag(Flags.Subtraction, false);
			SetFlag(Flags.HalfCarry, false);
			const int carryMask = 0x01;
			SetFlag(Flags.Carry, (oldVal & carryMask) != 0);
			Memory.Write(Registers[address], newByte);
		}
		void ShiftRightLogic(R8 reg)
		{
			byte oldVal = GetR8Byte(reg);
			int newVal = oldVal >>> 1;
			byte newByte = (byte)newVal;
			SetFlag(Flags.Zero, newByte == 0);
			SetFlag(Flags.Subtraction, false);
			SetFlag(Flags.HalfCarry, false);
			const int carryMask = 0x01;
			SetFlag(Flags.Carry, (oldVal & carryMask) != 0);
			SetR8Byte(reg, newByte);
		}
		void ShiftRightLogic(R16 address)
		{
			byte oldVal = Memory.Read(Registers[address]);
			int newVal = oldVal >>> 1;
			byte newByte = (byte)newVal;
			SetFlag(Flags.Zero, newByte == 0);
			SetFlag(Flags.Subtraction, false);
			SetFlag(Flags.HalfCarry, false);
			const int carryMask = 0x01;
			SetFlag(Flags.Carry, (oldVal & carryMask) != 0);
			Memory.Write(Registers[address], newByte);
		}
		void Swap(R8 reg)
		{
			byte oldVal = GetR8Byte(reg);
			byte newVal = (byte)((oldVal << 4) + (oldVal >>> 4));
			SetFlag(Flags.Zero, newVal == 0);
			SetFlag(Flags.Subtraction, false);
			SetFlag(Flags.HalfCarry, false);
			SetFlag(Flags.Carry, false);
			SetR8Byte(reg, newVal);
		}
		void Swap(R16 address)
		{
			byte oldVal = Memory.Read(Registers[address]);
			byte newVal = (byte)((oldVal << 4) + (oldVal >>> 4));
			SetFlag(Flags.Zero, newVal == 0);
			SetFlag(Flags.Subtraction, false);
			SetFlag(Flags.HalfCarry, false);
			SetFlag(Flags.Carry, false);
			Memory.Write(Registers[address], (byte)newVal);
		}
		#endregion
		#region Jumps and subroutine instructions
		/// <summary>
		/// CALL instruction
		/// </summary>
		/// <param name="address"></param>
		void Call(ushort address)
		{
			Push((ushort)(Registers[R16.PC] + 3));
			Jump(address);
		}
		/// <summary>
		/// Call instruction with condition
		/// </summary>
		/// <param name="address"></param>
		/// <param name="flag"></param>
		/// <param name="set"></param>
		void CallConditional(ushort address, Flags flag, bool set)
		{
			if (GetFlag(flag) == set)
			{
				ConditionalTicks = 12;
				Call(address);
			}
		}
		/// <summary>
		/// RST instruction
		/// </summary>
		/// <param name="address"></param>
		void Restart(ushort address)
		{
			Push((ushort)(Registers[R16.PC] + 1));
			Jump(address);
		}
		void Jump(R16 reg)
		{
			Jump(Registers[reg]);
		}
		void Jump(ushort address)
		{
			Load(R16.PC, address);
			PCAdvance = 0;
		}
		void JumpConditional(ushort address, Flags flag, bool set)
		{
			if (GetFlag(flag) == set)
			{
				ConditionalTicks = 4;
				Jump(address);
			}
		}
		void JumpRelative(byte value)
		{
			sbyte rel = (sbyte)value;
			int newPC = Registers[R16.PC] + rel;
			Registers[R16.PC] = (ushort)newPC;
			//PCAdvance = 0;
		}
		void JumpRelativeConditional(byte value, Flags flag, bool set)
		{
			if (GetFlag(flag) == set)
			{
				ConditionalTicks = 4;
				JumpRelative(value);
			}
		}
		void ReturnConditional(Flags flag, bool set)
		{
			if (GetFlag(flag) == set)
			{
				ConditionalTicks = 12;
				Return();
			}
		}
		void Return()
		{
			Pop(R16.PC);
			PCAdvance = 0;
		}
		/// <summary>
		/// IMMEDIATELY set IME, then return.
		/// </summary>
		void ReturnInterrupt()
		{
			InterruptMasterEnable = true;
			Return();
		}
		#endregion
		#region Carry flag instructions
		void ComplementCarryFlag()
		{
			SetFlag(Flags.Subtraction, false);
			SetFlag(Flags.HalfCarry, false);
			SetFlag(Flags.Carry, !GetFlag(Flags.Carry));
		}
		void SetCarryFlag()
		{
			SetFlag(Flags.Subtraction, false);
			SetFlag(Flags.HalfCarry, false);
			SetFlag(Flags.Carry, true);
		}
		#endregion
		#region Stack manipulation instructions
		/// <summary>
		/// <c>ADD SP, e8</c><br />
		/// Add a signed 8-bit value to SP.
		/// </summary>
		/// <param name="e8">Signed 8-bit value</param>
		void AddSP(byte e8)
		{
			int oldVal = Registers[R16.SP];
			int newVal = oldVal + (sbyte)e8;
			Registers[R16.SP] = (ushort)newVal;
			SetFlag(Flags.Zero, false);
			SetFlag(Flags.Subtraction, false); 
			SetFlag(Flags.HalfCarry, ((oldVal & 0x0f) + (e8 & 0x0f)) >= 0x10);
			SetFlag(Flags.Carry, (oldVal & 0xff) + e8 > 0xff);
		}
		/// <summary>
		/// <c>LD [n16],SP</c><br />
		/// Copy SP & $FF at address n16 and SP >> 8 at address n16 + 1.
		/// </summary>
		/// <param name="address"></param>
		void Loadn16SP(ushort address)
		{
			ushort sp = Registers[R16.SP];
			Memory.Write(address, (byte)sp);
			Memory.Write(address + 1, (byte)(sp >> 8));
		}
		/// <summary>
		/// <c>LD HL, SP + e8</c><br />
		/// Load SP plus a signed 8-bit value into HL.
		/// </summary>
		/// <param name="e8">Signed 8-bit value</param>
		void LoadHLrelSP(byte e8)
		{
			int oldVal = Registers[R16.SP];
			int val = oldVal + (sbyte)e8;
			Registers[R16.HL] = (ushort)val;
			SetFlag(Flags.Zero, false);
			SetFlag(Flags.Subtraction, false);
			SetFlag(Flags.HalfCarry, ((oldVal & 0x0f) + (e8 & 0x0f)) >= 0x10);
			SetFlag(Flags.Carry, (oldVal & 0xff) + e8 > 0xff);
		}
		void Load(R16 dest, R16 src)
		{
			Registers[dest] = Registers[src];
		}
		void Pop(R16 reg)
		{
			// I hope I did this correctly.
			byte b1 = Memory.Read(Registers[R16.SP] + 0);
			byte b2 = Memory.Read(Registers[R16.SP] + 1);
			Registers[reg] = (ushort)(b1 + (b2 << 8));
			Registers[R16.SP] += 2;
		}
		void Push(R16 reg)
		{
			// I hope I did this correctly.
			R8 r1 = (R8)reg;
			R8 r2 = (R8)(reg + 128);
			Memory.Write(Registers[R16.SP] - 1, GetR8Byte(r2));
			Memory.Write(Registers[R16.SP] - 2, GetR8Byte(r1));
			Registers[R16.SP] -= 2;
		}
		void Push(ushort value)
		{
			// I hope I did this correctly.
			Memory.Write(Registers[R16.SP] - 1, (byte)(value >> 8));
			Memory.Write(Registers[R16.SP] - 2, (byte)(value));
			Registers[R16.SP] -= 2;
		}
		#endregion
		#region Interrupt-related instructions
		void DisableInterrupt()
		{
			InterruptMasterEnable = false;
		}
		void EnableInterrupt()
		{
			InterruptMasterEnableQueue = true;
		}
		void Halt()
		{
			Halted = true;
		}
		#endregion
		#region Miscellaneous instructions
		void DecimalAdjustAccumulator()
		{
			// https://rgbds.gbdev.io/docs/v0.9.1/gbz80.7#DAA
			int adjust = 0;
			int addsub = 1;
			byte oldA = GetR8Byte(R8.A);
			if (GetFlag(Flags.Subtraction))
			{
				addsub = -1;
				if (GetFlag(Flags.HalfCarry))
				{
					adjust += 0x6;
				}
				if (GetFlag(Flags.Carry))
				{
					adjust += 0x60;
				}
			}
			else
			{
				if (GetFlag(Flags.HalfCarry) || (oldA & 0xf) > 0x9)
				{
					adjust += 0x6;
				}
				if (GetFlag(Flags.Carry) || oldA > 0x99)
				{
					adjust += 0x60;
					SetFlag(Flags.Carry, true);
				}
			}
			byte newval = (byte)(oldA + adjust * addsub);
			SetFlag(Flags.Zero, newval == 0);
			SetFlag(Flags.HalfCarry, false);
			SetR8Byte(R8.A, newval);
		}
		void NoOp()
		{

		}
		void Stop()
		{
			Stopped = true;
		}
		#endregion
	}
}
