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
		APU Audio;
		Joypad Input;
		Serial Serial;
		IORegisters IO;
		int Frame = -1;
		Stopwatch Time = new Stopwatch();
		public string GameTitle => Cart.Title;
		public GB(string cartPath, string savePath, nint renderer)
		public GB(GameBoyType gbType, string cartPath, string savePath, nint renderer)
		{
			Cart = Cartridge.FromFile(cartPath, savePath);
			if (gbType == GameBoyType.Auto)
			{
				gbType = Cart.CartType;
			}
			GameBoyType compatMode = GameBoyType.GameBoy;
			if (gbType == GameBoyType.GameBoyColor && Cart.CartType == GameBoyType.GameBoyColor)
			{
				compatMode = GameBoyType.GameBoyColor;
			}
			Display = new SDLDisplay(renderer);
			IO = new IORegisters();
			Input = new SDLJoypad(IO);
			Graphics = new PPU(IO, Display);
			Audio = new APU(IO);
			Serial = new Serial(IO);
			Memory = new Memory(Cart, Graphics, IO, compatMode);
			CPU = new CPU(Memory, compatMode);
			Time.Start();
		}
		public void Step()
		{
			int stepTicks = 0;
			Frame++;
			Input.FrameStep();
			while (stepTicks < PPU.TicksPerFrame)
			{
				CPU.Step();
				stepTicks += CPU.StepTicks;
				Serial.Step(CPU.TState);
				Input.CPUStep();
				Graphics.Step(CPU.TState);
				IO.StepTimerRegisters(CPU.TState);
				Audio.Step(CPU.TState);
			}
			Display.Output();
			if (Frame % 60 == 0)
			{
				Console.WriteLine($"Frame {Frame}, time: {Time.Elapsed}");
			}
		}
		/// <summary>
		/// Save the contents of cartridge RAM to a file on disk.
		/// </summary>
		/// <param name="savePath">Path to save to.</param>
		public void Save(string savePath)
		{
			Cart.SaveRAM(savePath);
		}
	}
	enum GameBoyType
	{
		Auto = -1,
		GameBoy = 0,
		GameBoyColor = 1
	}
}
