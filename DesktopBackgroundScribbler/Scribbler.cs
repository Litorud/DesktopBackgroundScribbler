using System;
using System.Drawing;

namespace DesktopBackgroundScribbler
{
    public class Scribbler
    {
        Random random = new Random();

        public void Scribble(string text, Graphics graphics, int width, int height)
        {
            // 画像のサイズから、最小フォントサイズを短辺の1/100と決定する。
            var 短辺 = Math.Min(width, height);
            var minFontSize = 短辺 / 100F;

            // フォントファミリーとフォントスタイルをランダムに決定する。
            var fontInfo = FontInfo.GenerateRandomFontInfo(text, random);

            // フォントファミリー、フォントスタイル、最小フォントサイズから、テキストパスサイズを求める。
            var textPath = new TextPath(text, fontInfo, minFontSize);

            var pathBounds = textPath.Path.GetBounds();

            // テキストパスサイズと画像サイズから、拡大率を決定する。
            var scaleRatio = GenerateRandomScaleRatio(pathBounds.Width, pathBounds.Height, width, height);

            // 傾きを決定する。
            var angle = GenerateRandomAngle(45);

            // 最小フォントサイズ、拡大率、テキストパスサイズ、画像サイズ、傾きから、位置を決定する。
            var point = GenerateRandomPoint(
                scaleRatio,
                pathBounds.Width, pathBounds.Height,
                width, height,
                angle);

            // 色を決定する。色に基づいて Brush を作成する。
            var color = GenerateRandomColor();

            // フォントサイズから、縁取りの幅を決定する。
            // フォントサイズ≒文字列高さなので、別に pathBounds.Height * scaleRatio を渡してもよい。
            var strokeWidth = GenerateRandomStrokeWidth(minFontSize * scaleRatio);

            // 実際に描画。
            Draw(graphics, textPath, scaleRatio, angle, point, color, strokeWidth);
        }

        private double GenerateRandomScaleRatio(float pathWidth, float pathHeight, int imageWidth, int imageHeight)
        {
            // 画像と同じ幅、または画像と同じ高さまで拡大することを許容する。
            var 最大拡大率 = Math.Min(imageWidth / pathWidth, imageHeight / pathHeight);

            // 1～最大拡大率の範囲でランダムな拡大率を返す。
            // ただし、最大拡大率が1未満の場合、1を返す。
            // 例えば、画像サイズが 200×100 の場合、最小フォントサイズは1だが、このフォントサイズでも201文字以上あれば文字列幅が画像幅より長くなる。
            // このとき、最大拡大率が1未満になる。
            if (最大拡大率 < 1)
            {
                return 1;
            }

            return (最大拡大率 - 1) * random.NextDouble() + 1;
        }

        private double GenerateRandomAngle(double maxAngle)
        {
            return random.NextDouble() * maxAngle * 2 - maxAngle;
        }

        private PointF GenerateRandomPoint(double scaleRatio, float pathWidth, float pathHeight, int imageWidth, int imageHeight, double angle)
        {
            var 文字列幅 = pathWidth * scaleRatio;
            var 文字列高さ = pathHeight * scaleRatio;
            var rad = angle * Math.PI / 180;

            var 描画幅 = Math.Abs(Math.Cos(rad)) * 文字列幅 + Math.Abs(Math.Sin(rad)) * 文字列高さ;
            var 描画高さ = Math.Abs(Math.Sin(rad)) * 文字列幅 + Math.Abs(Math.Cos(rad)) * 文字列高さ;
            var 描画幅の半分 = 描画幅 / 2;
            var 描画高さの半分 = 描画高さ / 2;

            // 開始位置及び終了位置は、文字列高さ（≒フォントサイズ）の3分の1までなら見切れてよい。
            var はみ出し許容量 = 文字列高さ / 3;

            double x, y;

            // 例えば、画像サイズが 200×100 の場合、最小フォントサイズは1だが、このフォントサイズでも201文字以上あれば文字列幅が画像幅より長くなる。
            // さらに、1000文字もあれば、はみ出し許容量をもってしても文字列幅のほうが明らかに長くなる。
            // このような場合は、はみ出し許容量よりも左のどの点から文字列描画が開始するか、を考えなくてはならない。
            // つまり、文字列幅 < 画像幅の場合（大抵はこっち）と、文字列幅 > 画像幅の場合とで、場合分けしなければならない。
            if (描画幅 <= はみ出し許容量 + imageWidth + はみ出し許容量) // 文字列幅 < 画像幅の場合
            {
                x = random.NextDouble() * imageWidth; // 画像全体の中から完全にランダムな座標。
                x = 押し込む(x, 描画幅の半分, imageWidth); // 描画がはみ出すなら、その分押し込む。
            }
            else // 文字列幅 > 画像幅の場合
            {
                var 左端限界 = imageWidth + はみ出し許容量 - 描画幅の半分;
                var 右端限界 = -はみ出し許容量 + 描画幅の半分;
                x = 左端限界 + random.NextDouble() * (右端限界 - 左端限界);
            }

            if (描画高さ < はみ出し許容量 + imageHeight + はみ出し許容量) // 文字列高さ < 画像高さの場合
            {
                y = random.NextDouble() * imageHeight; // 画像全体の中から完全にランダムな座標。
                y = 押し込む(y, 描画高さの半分, imageHeight); // 描画がはみ出すなら、その分押し込む。
            }
            else // 文字列高さ > 画像高さの場合
            {
                var 上端限界 = imageHeight + はみ出し許容量 - 描画高さの半分;
                var 下端限界 = -はみ出し許容量 + 描画高さの半分;
                y = 上端限界 + random.NextDouble() * (下端限界 - 上端限界);
            }

            return new PointF((float)x, (float)y);

            double 押し込む(double xx, double halfWidth, double imageLength)
            {
                var 左端限界 = halfWidth - はみ出し許容量;
                if (xx <= 左端限界)
                {
                    return 左端限界;
                }

                var 右端限界 = imageLength - halfWidth + はみ出し許容量;
                if (xx >= 右端限界)
                {
                    return 右端限界;
                }

                return xx;
            }
        }

        private Color GenerateRandomColor()
        {
            var r = random.Next(256);
            var g = random.Next(256);
            var b = random.Next(256);
            return Color.FromArgb(r, g, b);
        }

        private double GenerateRandomStrokeWidth(double fontSize)
        {
            // フォントサイズ 2.56px のとき1～1、
            // フォントサイズ 1080px のとき3～10 と決め、連立方程式を解いてリテラル値を求めた。
            var 最小幅 = 0.001856252 * fontSize + 0.995247995;
            var 最大幅 = 0.008353133 * fontSize + 0.978615979;
            return 最小幅 + (最大幅 - 最小幅) * random.NextDouble();
        }

        private void Draw(Graphics graphics, TextPath textPath, double scaleRatio, double angle, PointF point, Color color, double strokeWidth)
        {
            // 拡大率、傾き、位置に基づいて GraphicsPath を変換する。
            textPath.Transform(scaleRatio, angle, point);

            // 色から、縁取りの色を求める。
            var strokeColor = color.GetBrightness() > 0.90F ? Color.Black : Color.White;

            // 色、縁取りの色、縁取りの幅に基づいて Brush と Pen を作成する。
            var brush = new SolidBrush(color);
            var pen = new Pen(strokeColor, (float)strokeWidth);

            graphics.DrawPath(pen, textPath.Path);
            graphics.FillPath(brush, textPath.Path);
        }
    }
}
