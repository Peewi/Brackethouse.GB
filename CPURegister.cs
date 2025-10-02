using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brackethouse.GB
{
	/// <summary>
	/// CPU registers, accessible with indexers.
	/// </summary>
    class CPURegister
    {
		/// <summary>
		/// Used to prevent writes to unused part of AF.
		/// </summary>
		ushort[] RegisterMasks =
		[
			0xfff0,
			0xffff,
			0xffff,
			0xffff,
			0xffff,
			0xffff
		];
		readonly ushort[] RegisterData = new ushort[6];
		/// <summary>
		/// 16-bit register
		/// </summary>
		/// <param name="reg">The desired register</param>
		/// <returns>A 16-bit value</returns>
		public ushort this[R16 reg]
        {
            get => RegisterData[(int)reg];
            set => RegisterData[(int)reg] = (ushort)(value & RegisterMasks[(int)reg]);
		}
		/// <summary>
		/// 8-bit register, which is actually half of a 16-bit register.
		/// </summary>
		/// <param name="reg">The desired register</param>
		/// <returns>An 8-bit value</returns>
        public byte this[R8 reg]
		{
			get => GetR8Byte(reg);
			set => SetR8Byte(reg, value);
		}
		/// <summary>
		/// Flags, which are stored in the <see cref="R16.AF"/> register.
		/// </summary>
		/// <param name="flag">Desired flag</param>
		/// <returns>A bool</returns>
        public bool this[Flags flag]
		{
			get => GetFlag(flag);
			set => SetFlag(flag, value);
		}
		/// <summary>
		/// Set the registers to a specific state.
		/// </summary>
		/// <param name="state">State struct</param>
		public void SetState(CPURegisterState state)
		{
			this[R16.AF] = state.AF;
			this[R16.BC] = state.BC;
			this[R16.DE] = state.DE;
			this[R16.HL] = state.HL;
			this[R16.PC] = state.PC;
			this[R16.SP] = state.SP;
		}
		/// <summary>
		/// Get the current register state, in a struct
		/// </summary>
		/// <returns>State struct.</returns>
		public CPURegisterState GetState()
		{
			return new CPURegisterState()
			{
				AF = this[R16.AF],
				BC = this[R16.BC],
				DE = this[R16.DE],
				HL = this[R16.HL],
				PC = this[R16.PC],
				SP = this[R16.SP],
			};
		}

		/// <summary>
		/// Get a byte value from the 16-bit registers
		/// </summary>
		/// <param name="register">Which 8-bit register to access</param>
		/// <returns>The value that was stored.</returns>
		byte GetR8Byte(R8 register)
		{
			// I'm possibly trying to be more clever than neccessary.
			// R8 enum upper nibble indicates a register,and upper
			// nibble indicates how far to bitshift it to get the desired byte.
			// (0 or the 128 bit, which gets shifted to 8).
			const byte lowerMask = 0x0f;
			const byte upperMask = 0xf0;
			R16 reg = (R16)(lowerMask & (byte)register);
			byte shiftAmount = (byte)((upperMask & (byte)register) >> 4);
			ushort r16 = this[reg];
			return (byte)(r16 >> shiftAmount);
		}
		void SetR8Byte(R8 register, byte value)
		{
			const byte lowerMask = 0x0f;
			const byte upperMask = 0xf0;
			R16 reg = (R16)(lowerMask & (byte)register);
			byte shiftAmount = (byte)((upperMask & (byte)register) >> 4);

			ushort r16Val = this[reg];
			ushort mask16 = (ushort)(0xff00 >> shiftAmount);
			ushort shiftedValue = (ushort)(value << shiftAmount);
			r16Val = (ushort)(r16Val & mask16);
			r16Val = (ushort)(r16Val | shiftedValue);
			this[reg] = r16Val;
		}
		/// <summary>
		/// Get value of a flag
		/// </summary>
		/// <param name="flag">Flag to get.</param>
		/// <returns>Whether flag was set.</returns>
		bool GetFlag(Flags flag)
		{
			byte f = GetR8Byte(R8.F);
			return (f & (byte)flag) != 0;
		}
		/// <summary>
		/// Set a flag to a value.
		/// </summary>
		/// <param name="flag">Flag to set.</param>
		/// <param name="value">Value to set flag to.</param>
		void SetFlag(Flags flag, bool value)
		{
			byte f = GetR8Byte(R8.F);
			if (value)
			{
				f = (byte)(f | (byte)flag);
			}
			else
			{
				f = (byte)(f & ~(byte)flag);
			}
			SetR8Byte(R8.F, f);
		}
	}
}
