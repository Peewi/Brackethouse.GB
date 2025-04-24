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
        public abstract void SetPixel(byte x, byte y, byte value);
        public abstract void Output();
        public const int DisplayPixels = 160 * 144;
    }
    class SDLDisplay : Display
	{
		SDL.FPoint[][] PxCoords =
			[
			new SDL.FPoint[DisplayPixels],
			new SDL.FPoint[DisplayPixels],
			new SDL.FPoint[DisplayPixels],
			new SDL.FPoint[DisplayPixels],
			];
		int[] PxCounts = new int[4];
		uint FrameCount = 0;
        public SDLDisplay(nint renderer)
        {
            Renderer = renderer;
        }
        byte[] Shades = [0xff, 0xd3, 0xa9, 0x00, 0xbb];
        nint Renderer;
		public override void SetPixel(byte x, byte y, byte value)
		{
            PxCoords[value][PxCounts[value] % DisplayPixels] = new SDL.FPoint()
                {
                    X = x,
                    Y = y,
                };
            PxCounts[value]++;
		}
		public override void Output()
		{
            for (int i = 0; i < 4; i++)
			{
				SDL.SetRenderDrawColor(Renderer, Shades[i], Shades[i], Shades[i], 255);
                SDL.RenderPoints(Renderer, PxCoords[i], PxCounts[i]);
                PxCounts[i] = 0;
			}
#if DEBUG
            SDL.SetRenderDrawColor(Renderer, Shades[4], Shades[4], Shades[4], 255);
            SDL.RenderDebugText(Renderer, 1, 1, $"Frame {FrameCount++}");
#endif
            SDL.RenderPresent(Renderer);
			SDL.SetRenderDrawColor(Renderer, Shades[0], Shades[0], Shades[0], 255);
			SDL.RenderClear(Renderer);
		}
	}
}
