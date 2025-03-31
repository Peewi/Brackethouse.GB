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
    /// <summary>
    /// A simple display that outputs to a file.
    /// </summary>
    class DisplayFile : Display
    {
        Bitmap pic = new Bitmap(160, 144);
        Color[] Colors = [Color.White, Color.LightGray, Color.DarkGray, Color.Black];

        public override void SetPixel(byte x, byte y, byte value)
        {
            pic.SetPixel(x, y, Colors[value]);
			if (value != 0)
			{

			}
		}

        public override void Output()
        {
            pic.Save("test.bmp");
        }
    }
}
