using SDL3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brackethouse.GB
{
	/// <summary>
	/// https://gbdev.io/pandocs/Joypad_Input.html
	/// </summary>
	class Joypad
	{
		const ushort JoypadAddress = 0xff00;
        const byte HighNibble = 0xf0;
        const byte DPadSelect = 0b0001_0000;
        const byte ButtonSelect = 0b0010_0000;
		readonly IORegisters IO;
        /// <summary>
        /// Values for what buttons are pressed.
        /// </summary>
        protected byte ButtonsByte = 0x0f;
        /// <summary>
        /// Values for what is pressed on the d-pad.
        /// </summary>
        protected byte DPadByte = 0x0f;
        public Joypad(IORegisters io)
        {
            IO = io;
        }
        /// <summary>
        /// Executed every CPU cycle.
        /// </summary>
        public void CPUStep()
        {
            byte joyByte = IO[JoypadAddress];
            byte oldJoy = joyByte;
            joyByte &= HighNibble;
            if ((joyByte & DPadSelect) == 0)
            {
                joyByte |= DPadByte;
            }
			if ((joyByte & ButtonSelect) == 0)
			{
				joyByte |= ButtonsByte;
			}
            if ((joyByte & 0x30) == 0x30)
            {
                joyByte |= 0x0f;
			}
            IO[JoypadAddress] = joyByte;
            for (int i = 0; i < 4; i++)
            {
                int bitMask = 1 << i;
                if ((oldJoy & bitMask) != 0 && (joyByte & bitMask) == 0)
                {
                    FlagJoypadInterrupt();
                    break;
                }
            }
		}
        /// <summary>
        /// Executed every video frame.
        /// </summary>
        public virtual void FrameStep()
        {

        }
        void FlagJoypadInterrupt()
		{
			const int joypadBit = 0b0001_0000;
			IO[IORegisters.InterruptFlagAddress] |= joypadBit;
		}
    }

	class SDLJoypad(IORegisters io) : Joypad(io)
	{
        /// <summary>
        /// Keybindings for DPad Right, DPad Left, DPad Up, and DPad Down.
        /// </summary>
        int[] DPadBindings =
        {
            (int)SDL.Scancode.Right,
            (int)SDL.Scancode.Left,
            (int)SDL.Scancode.Up,
            (int)SDL.Scancode.Down,
		};
        /// <summary>
        /// Keybindings for A, B, Start, and Select.
        /// </summary>
        int[] ButtonBindings =
		{
			(int)SDL.Scancode.S,
			(int)SDL.Scancode.A,
			(int)SDL.Scancode.Backspace,
			(int)SDL.Scancode.Return,
		};
		public override void FrameStep()
		{
			base.FrameStep();
			bool[] kb = SDL.GetKeyboardState(out int numkeys);
            DPadByte = 0;
            for (int i = 0; i < 4; i++)
            {
                bool pressed = kb[DPadBindings[i]];
                byte btnVal = Convert.ToByte(!pressed);
                btnVal <<= i;
                DPadByte |= btnVal;
            }
			ButtonsByte = 0;
			for (int i = 0; i < 4; i++)
			{
				bool pressed = kb[ButtonBindings[i]];
				byte btnVal = Convert.ToByte(!pressed);
				btnVal <<= i;
				ButtonsByte |= btnVal;
			}
		}
	}
}
