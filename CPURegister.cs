using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brackethouse.GB
{
    class CPURegister
    {
		/// <summary>
		/// 16-bit register
		/// </summary>
		/// <param name="reg">The desired register</param>
		/// <returns>A 16-bit value</returns>
        public ushort this[R16 reg]
        {
            get => RegisterData[(int)reg];
            set => RegisterData[(int)reg] = value;
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
        ushort[] RegisterData = new ushort[6];

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
