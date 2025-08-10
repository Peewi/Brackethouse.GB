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
			IO = new IORegisters(gbType);
			Input = new SDLJoypad(IO);
			if (gbType == GameBoyType.GameBoy)
			{
				Graphics = new PPU(IO, Display);
			}
			else
			{
				Graphics = new PPUColor(IO, Display, compatMode);
			}
			Audio = new APU(IO);
			Serial = new Serial(IO);
			Memory = new Memory(Cart, Graphics, IO, compatMode);
			CPU = new CPU(Memory, compatMode);
			Time.Start();
		}
		public void Step()
		{
			int stepTicks = 0;
			Input.FrameStep();
			while (stepTicks < PPU.TicksPerFrame)
			{
				IO.BeforeCPUStep();
				CPU.Step();
				stepTicks += CPU.StepTicks;
				Serial.Step((ushort)(CPU.StepTicks << CPU.SpeedShift));
				Input.CPUStep();
				Graphics.Step(CPU.StepTicks);
				IO.StepTimerRegisters(CPU.StepTicks, CPU.SpeedShift);
				Audio.Step(CPU.StepTicks);
				if (Frame != Graphics.Frame)
				{
					Frame = Graphics.Frame;
					Display.Output();
				}
			}
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
