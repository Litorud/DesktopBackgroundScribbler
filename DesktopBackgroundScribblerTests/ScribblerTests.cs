using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace DesktopBackgroundScribbler.Tests
{
    [TestClass()]
    public class ScribblerTests
    {
        [TestMethod()]
        public void GenerateRandomScaleRatioTest()
        {
            var privateObject = new PrivateObject(new Scribbler());
            var methodName = Regex.Replace(MethodBase.GetCurrentMethod().Name, "Test$", string.Empty);

            // 1×1のパスを1920×1080の画像に描画するとき、拡大率は1～1080となることを確認する。
            // その他のパスを画像に描画するとき、拡大率は常に1となることを確認する。
            var imageWidth = 1920;
            var imageHeight = 1080;
            var patterns = new[]
            {
                (width: 1, height: 1, min: 1, max: 1080),
                (width: 1920, height: 1, min: 1, max: 1080),
                (width: 1, height: 1080, min: 1, max: 1080),
                (width: 1921, height: 1, min: 1, max: 1080),
                (width: 1, height: 2160, min: 1, max: 1080)
            };

            var times = 1000;
            var results = patterns.Select(p =>
            {
                var args = new object[] { p.width, p.height, imageWidth, imageHeight };
                var rs = Enumerable.Range(1, times)
                    .Select(_ => (double)privateObject.Invoke(methodName, args))
                    .ToArray();

                Assert.IsTrue(rs.Min() >= p.min);
                Assert.IsTrue(rs.Max() <= p.max);

                return (title: $"{p.width}×{p.height}", rs);
            });

            // 分布を調べるためにテキストに出力する。
            OutputResults(methodName, results);
        }

        [TestMethod()]
        public void GenerateRandomPointTest()
        {
            var privateObject = new PrivateObject(new Scribbler());
            var methodName = Regex.Replace(MethodBase.GetCurrentMethod().Name, "Test$", string.Empty);

            var times = 1000;

            // 1920×1080の画像に描画するとき、最小フォントサイズは10.8。
            // このフォントサイズのとき100×10.8となる文字列を、10倍に拡大して1000×108で描画する場合、
            // 文字列高さ=108の、3分の1=36までは、はみ出すことを許容するので、
            // 左端:  -36
            // 上端:  -36
            // 右端: 1956
            // 下端: 1116
            // に収まる範囲に描画する。
            // ということは、描画する文字列の中心点は (500, 54) なので、傾きを無視すれば、
            // 左端:  -36 + 500 =  464
            // 上端:  -36 +  54 =   18
            // 右端: 1956 - 500 = 1456
            // 下端: 1116 -  54 = 1062
            // が、中心点の取りうる範囲となる。

            var scaleRatio = 10D;
            var pathWidth = 100F;
            var pathHeight = 10.8F;
            var imageWidth = 1920;
            var imageHeight = 1080;
            var angle = 0D;
            var args = new object[] { scaleRatio, pathWidth, pathHeight, imageWidth, imageHeight, angle };

            var results0 = Enumerable.Range(1, times)
                .Select(_ => (PointF)privateObject.Invoke(methodName, args))
                .ToArray();

            Assert.IsTrue(results0.Min(r => r.X) >= 464);
            Assert.IsTrue(results0.Min(r => r.Y) >= 18);
            Assert.IsTrue(results0.Max(r => r.X) <= 1456);
            Assert.IsTrue(results0.Max(r => r.Y) <= 1062);

            // また、この文字列が30°傾く場合、外接する矩形の大きさは、
            // 幅　 = 1000×cos(30°)+108×sin(30°) = 866.02540378443864676372317075294+54 = 920.02540378443864676372317075294
            // 高さ = 1000×sin(30°)+108×cos(30°) = 500+93.530743608719373850482102441317 = 593.53074360871937385048210244132
            // となる。
            // ということは、描画する文字列の中心点は (460.01270189221932338186158537647, 296.76537180435968692524105122066) なので、
            // 左端:  -36 + 460.01270189221932338186158537647 =  424.01270189221932338186158537647
            // 上端:  -36 + 296.76537180435968692524105122066 =  260.76537180435968692524105122066
            // 右端: 1956 - 460.01270189221932338186158537647 = 1495.9872981077806766181384146235
            // 下端: 1116 - 296.76537180435968692524105122066 =  819.23462819564031307475894877934
            // が、中心点の取りうる範囲となる。

            angle = 30;
            args = new object[] { scaleRatio, pathWidth, pathHeight, imageWidth, imageHeight, angle };

            var results30 = Enumerable.Range(1, times)
                .Select(_ => (PointF)privateObject.Invoke(methodName, args))
                .ToArray();

            Assert.IsTrue(results30.Min(r => r.X) >= 424.01270189221932338186158537647F); // なぜか末尾にFを付けないとテストに失敗する。
            Assert.IsTrue(results30.Min(r => r.Y) >= 260.76537180435968692524105122066);
            Assert.IsTrue(results30.Max(r => r.X) <= 1495.9872981077806766181384146235F); // 末尾にFを付けないとテストに失敗する。
            Assert.IsTrue(results30.Max(r => r.Y) <= 819.23462819564031307475894877934);

            // 同様に、この文字列が-90°傾く場合、
            // 幅　 = 1000×cos(-90°)+108×sin(-90°) = 0+108 = 108
            // 高さ = 1000×sin(-90°)+108×cos(-90°) = 1000+0 = 1000
            // で、描画する文字列の中心点は (54, 500) なので、
            // 左端:  -36 +  54 =   18
            // 上端:  -36 + 500 =  464
            // 右端: 1956 -  54 = 1902
            // 下端: 1116 - 500 =  616
            // が、中心点の取りうる範囲となる。

            angle = -90;
            args = new object[] { scaleRatio, pathWidth, pathHeight, imageWidth, imageHeight, angle };

            var results90 = Enumerable.Range(1, times)
                .Select(_ => (PointF)privateObject.Invoke(methodName, args))
                .ToArray();

            Assert.IsTrue(results90.Min(r => r.X) >= 18);
            Assert.IsTrue(results90.Min(r => r.Y) >= 464);
            Assert.IsTrue(results90.Max(r => r.X) <= 1902);
            Assert.IsTrue(results90.Max(r => r.Y) <= 616);

            // 次に、最小フォントサイズ10.8で描画しても許容範囲以上に画像からはみ出すような、長い文字列を考える。
            // これは、幅が3.6+1920+3.6 = 1927.2となるような文字列が該当する。
            // この場合、中心点のX座標は、画像の中央=960しか取らない。
            // 一方、Y座標は-3.6+5.4～1080+3.6-5.4 = 1.8～1078.2 の範囲を取る。

            scaleRatio = 1;
            pathWidth = 1927.2F;
            pathHeight = 10.8F;
            angle = 0;
            args = new object[] { scaleRatio, pathWidth, pathHeight, imageWidth, imageHeight, angle };

            var results19272 = Enumerable.Range(1, times)
                .Select(_ => (PointF)privateObject.Invoke(methodName, args))
                .ToArray();

            Assert.AreEqual(960, results19272.Min(r => r.X));
            Assert.IsTrue(results19272.Min(r => r.Y) >= 1.8);
            Assert.AreEqual(960, results19272.Max(r => r.X));
            Assert.IsTrue(results19272.Max(r => r.Y) <= 1078.2);

            // また、このような文字列が傾くと、さらに長い幅を取る。
            // この場合は、-3.6よりさらに左側が、文字列の左端となる。
            // 例えば、0.3°傾いた場合、外接する矩形の幅は、
            // 1927.173582419240914874843878741+0.05654840937933146501261143619747 = 1927.2301308286202463398564901772
            // となる。
            // したがって、中心点のX座標がとる値は、960 ± 0.0301308286202463398564901772 となる。

            angle = 0.3;
            args = new object[] { scaleRatio, pathWidth, pathHeight, imageWidth, imageHeight, angle };

            var results192723 = Enumerable.Range(1, times)
                .Select(_ => (PointF)privateObject.Invoke(methodName, args))
                .ToArray();

            Assert.IsTrue(results192723.Min(r => r.X) >= 960 - 0.0301308286202463398564901772);
            Assert.IsTrue(results192723.Min(r => r.Y) >= 1.8);
            Assert.IsTrue(results192723.Max(r => r.X) <= 960 + 0.0301308286202463398564901772);
            Assert.IsTrue(results192723.Max(r => r.Y) <= 1078.2);

            // 分布を調べるためテキストに出力する。
            var results = new[]
            {
                (title: "0°", rs: results0),
                (title: "30°", rs: results30),
                (title: "-90°", rs: results90),
                (title: "x=1927.2", rs: results19272),
                (title: "x=1927.2&0.3°", rs: results192723)
            }.SelectMany(r => new[]
            {
                (title: $"{r.title}X", rs: r.rs.Select(rr => (double)rr.X).ToArray()),
                (title: $"{r.title}Y", rs: r.rs.Select(rr => (double)rr.Y).ToArray())
            });
            OutputResults(methodName, results);
        }

        [TestMethod()]
        public void GenerateRandomStrokeWidthTest()
        {
            var privateObject = new PrivateObject(new Scribbler());
            var methodName = Regex.Replace(MethodBase.GetCurrentMethod().Name, "Test$", string.Empty);

            // フォントサイズが2.56のとき、約1しか取らないこと。
            // フォントサイズが1080のとき、約3～10の範囲を取ること。
            var patterns = new[]
            {
                (fontSize: 2.56, min: 0.99999999948, max: 1.00000000012), // フォントサイズが小さいとき、min と max は逆になる。
                (fontSize: 1080, min: 3.000000155, max: 9.999999619)
            };

            var times = 1000;
            var results = patterns.Select(p =>
            {
                var args = new object[] { p.fontSize };
                var rs = Enumerable.Range(1, times)
                    .Select(_ => (double)privateObject.Invoke(methodName, args))
                    .ToArray();

                Assert.IsTrue(rs.Min() >= p.min);
                Assert.IsTrue(rs.Max() <= p.max);

                return (title: $"{p.fontSize}px", rs);
            });

            // 分布を調べるためにテキストに出力する。
            OutputResults(methodName, results);
        }

        static void OutputResults(string methodName, IEnumerable<(string title, double[] rs)> results)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join("\t", results.Select(r => r.title)));

            var length = results.Min(r => r.rs.Length);
            for (int i = 0; i < length; i++)
            {
                sb.AppendLine(string.Join("\t", results.Select(r => r.rs[i])));
            }

            var fileName = $"{methodName}TestResult.txt";
            var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);
            File.WriteAllText(filePath, sb.ToString());
        }
    }
}