using Microsoft.VisualStudio.TestTools.UnitTesting;
using DesktopBackgroundScribbler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Reflection;
using System.IO;

namespace DesktopBackgroundScribbler.Tests
{
    [TestClass()]
    public class TextPathTests
    {
        [TestMethod()]
        public void TransformTest()
        {
            var fontInfo = new FontInfo(FontFamily.GenericSansSerif, FontStyle.Regular);

            var scaleRatios = new[] { 100F, 10F, 1F };

            var points = new[]
            {
                new PointF(0, 0),
                new PointF(0, 1080),
                new PointF(1920, -30),
                new PointF(1950, 1110)
            };

            foreach (var scaleRatio in scaleRatios)
            {
                using (var bitmap = new Bitmap(1920, 1080))
                {
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        foreach (var point in points)
                        {
                            for (int i = -180; i < 180; i += 30)
                            {
                                var textPath = new TextPath("△--+--▽", fontInfo, 10.8F);
                                textPath.Transform(scaleRatio, i, point);

                                graphics.DrawPath(Pens.White, textPath.Path);
                            }
                        }
                    }

                    var fileName = MethodBase.GetCurrentMethod().Name + $"Result{scaleRatio}.bmp";
                    var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);

                    bitmap.Save(filePath);
                    Assert.IsTrue(File.Exists(filePath));
                }
            }
        }
    }
}