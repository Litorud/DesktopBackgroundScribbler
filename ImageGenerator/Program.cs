using DesktopBackgroundScribbler;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageGenerator
{
    class Program
    {
        static Scribbler scribbler = new Scribbler();

        const int width = 256;
        const int height = 256;

        static readonly IEnumerable<string> texts = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
            .Reverse()
            .Select(c => c.ToString());

        static void Main(string[] args)
        {
            for (int i = 0; i < 100; i++)
            {
                Generate($"Result{i}.png");
            }
        }

        private static void Generate(string fileName)
        {
            using (var bitmap = new Bitmap(width, height))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.SmoothingMode = SmoothingMode.HighQuality;

                    for (int i = 0; i < 10; i++)
                    {
                        foreach (var text in texts)
                        {
                            scribbler.Scribble(text, graphics, width, height);
                        }
                    }
                }

                bitmap.Save(fileName, ImageFormat.Png);
            }
        }
    }
}
