using SDL3;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brackethouse.GB
{
	/// <summary>
	/// Thing for getting pixels to somewhere I can look at them.
	/// </summary>
	abstract class Display
	{
		public abstract void SetPixel(byte x, byte y, Color value);
		public abstract void Output();
		public const int DisplayWidth = PPU.Width;
		public const int DisplayHeight = PPU.Height;
		public const int DisplayPixels = DisplayWidth * DisplayHeight;
	}
	struct Color(byte r, byte g, byte b)
	{
		public byte Red = r;
		public byte Green = g;
		public byte Blue = b;
		public static Color FromRGB555(ushort data)
		{
			byte r = (byte)(data & 0x1f);
			byte g = (byte)((data >> 5) & 0x1f);
			byte b = (byte)((data >> 10) & 0x1f);
			r <<= 3;
			g <<= 3;
			b <<= 3;
			return new Color(r, g, b);
		}
	}
	class SDLDisplay : Display
	{
		Color DebugTextColor = new Color(0xff, 0xbb, 0xbb);
		SDL.Vertex[] ScreenVerts = new SDL.Vertex[DisplayPixels * 3];
		uint FrameCount = 0;
		public SDLDisplay(nint renderer)
		{
			Renderer = renderer;
		}
		nint Renderer;
		public override void SetPixel(byte x, byte y, Color value)
		{
			SDL.FColor fCol = new SDL.FColor(value.Red / 255f, value.Green / 255f, value.Blue / 255f, 1);
			int i = DisplayWidth * y + x;
			ScreenVerts[i * 3 + 0] = new SDL.Vertex()
			{
				Color = fCol,
				Position = new SDL.FPoint()
				{
					X = x,
					Y = y
				}
			};
			ScreenVerts[i * 3 + 1] = new SDL.Vertex()
			{
				Color = fCol,
				Position = new SDL.FPoint()
				{
					X = x + 2,
					Y = y
				}
			};
			ScreenVerts[i * 3 + 2] = new SDL.Vertex()
			{
				Color = fCol,
				Position = new SDL.FPoint()
				{
					X = x,
					Y = y + 2
				}
			};
		}
		public override void Output()
		{
			SDL.RenderGeometry(Renderer, 0, ScreenVerts, ScreenVerts.Length, 0, 0);
#if DEBUG
			SDL.SetRenderDrawColor(Renderer, DebugTextColor.Red, DebugTextColor.Green, DebugTextColor.Blue, 255);
			SDL.RenderDebugText(Renderer, 1, 1, $"Frame {FrameCount++}");
#endif
			SDL.RenderPresent(Renderer);
			SDL.SetRenderDrawColor(Renderer, 0, 0, 0, 255);
			SDL.RenderClear(Renderer);
		}
	}
}
