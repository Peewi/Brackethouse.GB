using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brackethouse.GB
{
	class GB
	{
		Cartridge Cart;
		Display Display;
		CPU CPU;
		Memory Memory;
		PPU Graphics;
		Joypad Input;
		IORegisters IO;
		int Frame = -1;
		Stopwatch Time = new Stopwatch();
		public GB(string cartPath, nint renderer)
		{
			Cart = Cartridge.FromFile(cartPath);
			Display = new SDLDisplay(renderer);
			IO = new IORegisters();
			Input = new Joypad(IO);
			Graphics = new PPU(IO, Display);
			Memory = new Memory(Cart, Graphics, IO);
			CPU = new CPU(Memory);
			Time.Start();
		}
		public void Step()
		{
			Frame++;
			while (Frame == Graphics.Frame)
			{
				CPU.Step();
				Input.CPUStep();
				Graphics.Step(CPU.TState);
				IO.StepTimerRegisters(CPU.TState);
			}
			Display.Output();
			if (Frame % 60 == 0)
			{
				Console.WriteLine($"Frame {Frame}, time: {Time.Elapsed}");
			}
		}
	}
}
