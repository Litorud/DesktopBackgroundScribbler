using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DesktopBackgroundScribbler
{
    public class TextPath
    {
        static StringFormat stringFormat = new StringFormat(StringFormat.GenericDefault)
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        public GraphicsPath Path { get; }

        public TextPath(string text, FontInfo fontInfo, float fontSize)
        {
            Path = new GraphicsPath(FillMode.Winding);
            // AddPath() のフォントサイズはピクセル単位で指定することに注意。
            Path.AddString(text, fontInfo.Family, (int)fontInfo.Style, fontSize, Point.Empty, stringFormat);
        }

        public void Transform(double scaleRatio, double angle, PointF point)
        {
            var matrix = new Matrix();
            matrix.Translate(point.X, point.Y);

            var scaleRatioF = (float)scaleRatio;
            matrix.Scale(scaleRatioF, scaleRatioF);

            matrix.Rotate((float)angle);

            Path.Transform(matrix);
        }
    }
}
