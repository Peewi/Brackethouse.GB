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
		CPU CPU;
		Memory Memory;
		PPU Graphics;
		int Frame = -1;
		Stopwatch Time = new Stopwatch();
		public GB(string cartPath)
		{
			Memory = new Memory(cartPath);
			CPU = new CPU(Memory);
			Graphics = new PPU(Memory);
			Time.Start();
		}
		public void Step()
		{
			CPU.Step();
			Graphics.Step(CPU.TState);
			Memory.StepTimerRegisters(CPU.TState);
			if (Frame != Graphics.Frame)
			{
				Frame = Graphics.Frame;
				if (Frame % 60 == 0)
				{
					Console.WriteLine($"Frame {Frame}, time: {Time.Elapsed}");
				}
			}
		}
	}
}
