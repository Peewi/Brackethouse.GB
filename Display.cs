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
    }
    class SDLDisplay : Display
    {
        uint FrameCount = 0;
        public SDLDisplay(nint renderer)
        {
            Renderer = renderer;
        }
        byte[] Shades = [0xff, 0xd3, 0xa9, 0x00, 0xbb];
        nint Renderer;
		public override void SetPixel(byte x, byte y, byte value)
		{
			SDL.SetRenderDrawColor(Renderer, Shades[value], Shades[value], Shades[value], 255);
			SDL.RenderPoint(Renderer, x, y);
		}
		public override void Output()
		{
#if DEBUG
            SDL.SetRenderDrawColor(Renderer, Shades[4], Shades[4], Shades[4], 255);
            SDL.RenderDebugText(Renderer, 1, 1, $"Frame {FrameCount++}");
#endif
            SDL.RenderPresent(Renderer);
		}
	}
}
