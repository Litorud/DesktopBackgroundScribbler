using Microsoft.VisualStudio.TestTools.UnitTesting;
using DesktopBackgroundScribbler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Drawing;

namespace DesktopBackgroundScribbler.Tests
{
    [TestClass()]
    public class MainModelTests
    {
        [TestMethod()]
        public void ScribbleTest()
        {
            var mainModel = new MainModel();
            var texts = new[] { ".", "l", "鑑", "安以宇衣於加幾久計己左之寸世曽太知川天止奈仁奴祢乃波比不部保末美武女毛也以由江与良利留礼呂和為宇恵遠" };
            for (var i = 0; i < 50; i++)
            {
                foreach (var text in texts)
                {
                    mainModel.Scribble(text);
                    System.Threading.Thread.Sleep(100);
                }
            }
            Assert.IsTrue(true);
        }

        [TestMethod()]
        public void GenerateRandomScaleRatioTest()
        {
            var privateObject = new PrivateObject(new MainModel());
            var methodName = Regex.Replace(MethodBase.GetCurrentMethod().Name, "Test$", string.Empty);

            var pathWidths = new[] { 1F, 10.79F, 10.8F, 10.81F, 1920F, 3840F };
            foreach (var pathWidth in pathWidths)
            {
                var minFontSize = 10.8F;
                var maxFontSize = 2160;
                var pathHeight = 10.8F;
                var imageWidth = 1920;
                var imageHeight = 1080;
                var random = new Random();
                var args = new object[] { minFontSize, maxFontSize, pathWidth, pathHeight, imageWidth, imageHeight, random };

                var results = new List<double>();
                for (int i = 0; i < 1000; i++)
                {
                    var result = (double)privateObject.Invoke(methodName, args);
                    results.Add(result);
                }

                // 分布を調べるためにテキストに出力する。
                OutputResults(methodName, results.Select(d => d.ToString()), pathWidth);

                // 最小値
                Assert.IsTrue(results.Min() > 0);

                // 最大値
                Assert.IsTrue(results.Max() <= 200);
            }
        }

        static void OutputResults(string methodName, IEnumerable<string> results, object partName)
        {
            var fileName = $"{methodName}TestResult{partName}.txt";
            var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);
            File.WriteAllLines(filePath, results.Select(o => Convert.ToString(o)));
        }

        [TestMethod()]
        public void GenerateRandomPointTest()
        {
            var privateObject = new PrivateObject(new MainModel());
            var methodName = Regex.Replace(MethodBase.GetCurrentMethod().Name, "Test$", string.Empty);

            var angles = new[] { 0D, 45D, 90D };
            foreach (var angle in angles)
            {
                var minFontSize = 10F;
                var scaleRatio = 10D;
                var pathWidth = 100F;
                var pathHeight = 10F;
                var imageWidth = 1920;
                var imageHeight = 1080;
                var random = new Random();
                var args = new object[] { minFontSize, scaleRatio, pathWidth, pathHeight, imageWidth, imageHeight, angle, random };

                var xResults = new List<float>();
                var yResults = new List<float>();
                for (int i = 0; i < 3000; i++)
                {
                    var result = (PointF)privateObject.Invoke(methodName, args);
                    xResults.Add(result.X);
                    yResults.Add(result.Y);
                }

                // 分布を調べるためにテキストに出力する。
                OutputResults(methodName, xResults.Select(f => f.ToString()), angle + "X");
                OutputResults(methodName, yResults.Select(f => f.ToString()), angle + "Y");

                // 最小X, 最大X
                Assert.IsTrue(xResults.Min() >= 0);
                Assert.IsTrue(xResults.Max() <= 1920);

                // 最小Y, 最大Y
                Assert.IsTrue(yResults.Min() >= 0);
                Assert.IsTrue(yResults.Max() <= 1080);
            }
        }

        [TestMethod()]
        public void GenerateRandomStrokeWidthTest()
        {
            var privateObject = new PrivateObject(new MainModel());
            var methodName = Regex.Replace(MethodBase.GetCurrentMethod().Name, "Test$", string.Empty);

            var scaleRatios = new[] { 0.5, 1D, 2D, 4D, 100D, 200D };
            foreach (var scaleRatio in scaleRatios)
            {
                var random = new Random();
                var args = new object[] { scaleRatio, random };

                var results = new List<double>();
                for (int i = 0; i < 1000; i++)
                {
                    var result = (double)privateObject.Invoke(methodName, args);
                    results.Add(result);
                }

                // 分布を調べるためにテキストに出力する。
                OutputResults(methodName, results.Select(d => d.ToString()), scaleRatio);

                // 最小値
                // フォントサイズが10のとき、最小値は1。
                Assert.IsTrue(results.Min() >= 0);

                // 最大値
                // フォントサイズが2160とき、最大値は100とちょっと。
                Assert.IsTrue(results.Max() <= 105);
            }
        }

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