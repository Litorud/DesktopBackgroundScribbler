using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DesktopBackgroundScribbler
{
    public class Scribbler
    {
        Random random = new Random();

        public void Scribble(string text, Graphics graphics, int width, int height)
        {
            // 画像のサイズから、最小フォントサイズを短辺の1/100と決定する。また、最大フォントサイズを短辺の2倍と決定する。
            var 短辺 = Math.Min(width, height);
            var minFontSize = 短辺 / 100F;
            var maxFontSize = 短辺 * 2F;

            // フォントファミリーとフォントスタイルをランダムに決定する。
            var fontInfo = FontInfo.GenerateRandomFontInfo(text, random);

            // フォントファミリー、フォントスタイル、最小フォントサイズから、テキストパスサイズを求める。
            var textPath = new TextPath(text, fontInfo, minFontSize);

            var pathBounds = textPath.Path.GetBounds();

            // 最小フォントサイズ、最大フォントサイズ、テキストパスサイズ、画像サイズから、拡大率を決定する。
            var scaleRatio = GenerateRandomScaleRatio(
                minFontSize, maxFontSize,
                pathBounds.Width, pathBounds.Height,
                width, height);

            // 傾きを決定する。
            var angle = GenerateRandomAngle(45);

            // 最小フォントサイズ、拡大率、テキストパスサイズ、画像サイズ、傾きから、位置を決定する。
            var point = GenerateRandomPoint(
                minFontSize,
                scaleRatio,
                pathBounds.Width, pathBounds.Height,
                width, height,
                angle);

            // 色を決定する。色に基づいて Brush を作成する。
            var color = GenerateRandomColor();

            // 拡大率から、縁取りの幅を決定する。
            var strokeWidth = GenerateRandomStrokeWidth(scaleRatio);

            // 実際に描画。
            Draw(graphics, textPath, scaleRatio, angle, point, color, strokeWidth);
        }

        private double GenerateRandomScaleRatio(float minFontSize, float maxFontSize, float pathWidth, float pathHeight, int imageWidth, int imageHeight)
        {
            // 現文字列幅 × 最大拡大率 = 画像幅 + フォントサイズ
            // ここで、フォントサイズも拡大率に伴って変わるので、
            // 現文字列幅 × 最大拡大率 = 画像幅 + minFontSize × 最大拡大率
            // 最大拡大率 = 画像幅 ÷ (現文字列幅 - minFontSize)
            var 最大拡大率 = maxFontSize / minFontSize;
            if (pathWidth > minFontSize)
            {
                var 幅最大拡大率 = imageWidth / (pathWidth - minFontSize);
                if (pathHeight > minFontSize)
                {
                    var 高さ最大拡大率 = imageHeight / (pathHeight - minFontSize);
                    最大拡大率 = Math.Min(Math.Min(幅最大拡大率, 高さ最大拡大率), 最大拡大率);
                }
                else // pathHeight <= minFontSize の場合
                {
                    最大拡大率 = Math.Min(幅最大拡大率, 最大拡大率);
                }
            }
            else if (pathHeight > minFontSize)
            {
                var 高さ最大拡大率 = imageHeight / (pathHeight - minFontSize);
                最大拡大率 = Math.Min(高さ最大拡大率, 最大拡大率);
            }

            if (最大拡大率 <= 1)
            {
                return 最大拡大率;
            }

            // 1～最大拡大率の範囲でランダムで拡大率を決定する。
            return (最大拡大率 - 1) * random.NextDouble() + 1;
        }

        private double GenerateRandomAngle(double maxAngle)
        {
            return random.NextDouble() * maxAngle * 2 - maxAngle;
        }

        private PointF GenerateRandomPoint(float minFontSize, double scaleRatio, float pathWidth, float pathHeight, int imageWidth, int imageHeight, double angle)
        {
            // 位置を決定する。開始位置及び終了位置は、フォントサイズの半分までなら見切れてよい。
            var fontSize = minFontSize * scaleRatio;
            var halfFontSize = fontSize / 2;

            var 幅 = pathWidth * scaleRatio;
            var 高さ = pathHeight * scaleRatio;
            var rad = angle * Math.PI / 180;

            var 描画時に必要な幅 = Math.Abs(Math.Cos(rad)) * 幅 + Math.Abs(Math.Sin(rad)) * 高さ;
            var 取りうる幅 = imageWidth + fontSize - 描画時に必要な幅;
            var x = 取りうる幅 * random.NextDouble() - halfFontSize + 描画時に必要な幅 / 2;

            var 描画時に必要な高さ = Math.Abs(Math.Sin(rad)) * 幅 + Math.Abs(Math.Cos(rad)) * 高さ;
            var 取りうる高さ = imageHeight + fontSize - 描画時に必要な高さ;
            var y = 取りうる高さ * random.NextDouble() - halfFontSize + 描画時に必要な高さ / 2;

            return new PointF((float)x, (float)y);
        }

        private Color GenerateRandomColor()
        {
            var r = random.Next(256);
            var g = random.Next(256);
            var b = random.Next(256);
            return Color.FromArgb(r, g, b);
        }

        private double GenerateRandomStrokeWidth(double scaleRatio)
        {
            // scaleRatio が1（=フォントサイズが10.8）のとき、1～3の範囲でランダムに幅を決めたい。
            // scaleRatio が100（=フォントサイズが1080）のとき、3～10の範囲でランダムに幅を決めたい。
            // 連立方程式解いて定数を求めた。
            var 振れ幅 = 5 * scaleRatio / 99 + 1.9;
            var オフセット = 2 * scaleRatio / 99 + 1;
            // 上記式の第2項は、毎回割り算が発生するコストが見合わないので、
            // 近似値のリテラルで置き換えている。

            return 振れ幅 * random.NextDouble() + オフセット;

            // 別の求め方として、許容する最小の幅と最大の幅を計算し、
            // (最大幅 - 最小幅) * random.NextDouble() + 最小幅 を返す方法もある。
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
