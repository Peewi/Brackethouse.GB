using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brackethouse.GB
{
    class Joypad
	{
		const ushort JoypadAddress = 0xff00;
		IORegisters IO;
        public Joypad(IORegisters io)
        {
            IO = io;
        }
        public void CPUStep()
        {
            // TODO: real game input.
            // This makes it so no buttons are read as being pressed.
            byte joyByte = IO[JoypadAddress];
            joyByte |= 0x0f;
            IO[JoypadAddress] = joyByte;


		}
    }
}
