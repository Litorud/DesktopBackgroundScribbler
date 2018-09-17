using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DesktopBackgroundScribbler
{
    internal class DesktopBackgroundImage
    {
        public Bitmap Bitmap { get; }
        public Graphics Graphics { get; }

        public DesktopBackgroundImage()
        {
            var bounds = Screen.PrimaryScreen.Bounds;
            Bitmap = new Bitmap(bounds.Width, bounds.Height);

            Graphics = Graphics.FromImage(Bitmap);

            // 背景色で初期化する。
            // デスクトップの背景の画像が「ページ幅に合わせる」などの場合、背景色で初期化する必要は今のところ無いが、
            // 透過を含む画像が将来的に対応される可能性を考慮した。
            var rgb = GetBackgroundRgb().Take(3).ToArray();
            Graphics.Clear(Color.FromArgb(rgb[0], rgb[1], rgb[2]));

            using (var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop"))
            {
                var wallpaper = Convert.ToString(key.GetValue("Wallpaper"));
                if (!File.Exists(wallpaper))
                {
                    return;
                }

                try
                {
                    var wallpaperBitmap = new Bitmap(wallpaper);

                    // (TileWallpaper, WallpaperStyle)
                    // ページ幅に合わせる　　: (0, 10)
                    // 画面のサイズに合わせる: (0,  6)
                    // 拡大して表示　　　　　: (0,  2)
                    // 並べて表示　　　　　　: (1,  0)
                    // 中央に表示　　　　　　: (0,  0)
                    // スパン　　　　　　　　: (0, 22)
                    // 参考: https://smdn.jp/programming/tips/setdeskwallpaper/
                    int tile;
                    if (int.TryParse(Convert.ToString(key.GetValue("TileWallpaper")), out tile) && tile == 1)
                    {
                        TileImage(wallpaperBitmap);
                    }
                    else
                    {
                        int style;
                        if (!int.TryParse(Convert.ToString(key.GetValue("WallpaperStyle")), out style))
                            style = 10;

                        switch (style)
                        {
                            case 0: // 中央に表示
                                CenterImage(wallpaperBitmap);
                                break;
                            case 2: // 画面に合わせて伸縮
                                StretchImage(wallpaperBitmap);
                                break;
                            case 6: // ページ縦幅に合わせる
                                FitImage(wallpaperBitmap);
                                break;
                            default: // ページ横幅に合わせる
                                FillImage(wallpaperBitmap);
                                break;
                        }
                    }
                }
                catch
                {
                    return;
                }
            }
        }

        private IEnumerable<int> GetBackgroundRgb()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Colors"))
            {
                var backgroundParts = Convert.ToString(key.GetValue("Background"))
                    .Split(default(char[]), StringSplitOptions.RemoveEmptyEntries);
                foreach (var backgroundPart in backgroundParts)
                {
                    int i;
                    if (int.TryParse(backgroundPart, out i))
                    {
                        yield return i < 0 ? 0 : i > 255 ? 255 : i;
                    }
                }
            }

            while (true)
            {
                yield return 0;
            }
        }

        private void TileImage(Image image)
        {
            for (int y = 0; y < Bitmap.Height; y += image.Height)
            {
                for (int x = 0; x < Bitmap.Width; x += image.Width)
                    Graphics.DrawImage(image, x, y, image.Width, image.Height);
            }
        }

        private void CenterImage(Image image)
        {
            var x = (Bitmap.Width - image.Width) / 2;
            var y = (Bitmap.Height - image.Height) / 2;
            Graphics.DrawImage(image, x, y, image.Width, image.Height);
        }

        private void StretchImage(Image image)
        {
            // 少々強引だが、幅と高さをこのように調整すれば過不足無くずれを吸収できる。
            var width = Bitmap.Width + Bitmap.Width / (float)image.Width - 1;
            var height = Bitmap.Height + Bitmap.Height / (float)image.Height - 1;
            Graphics.DrawImage(image, 0, 0, width, height);
        }

        private void FitImage(Image image)
        {
            double x比率 = Bitmap.Width / (double)image.Width;
            double y比率 = Bitmap.Height / (double)image.Height;
            if (x比率 < y比率) // 横長の画像向け。
            {
                double bgh = image.Height * x比率;
                double 調整値 = x比率 - 1;
                var y = (int)((Bitmap.Height - bgh) / 2);
                var width = (float)(Bitmap.Width + 調整値);
                var height = (float)(bgh + 調整値);
                Graphics.DrawImage(image, 0, y, width, height);
            }
            else if (x比率 > y比率) // 縦長の画像向け。
            {
                double bgw = image.Width * y比率;
                double 調整値 = y比率 - 1;
                var x = (int)((Bitmap.Width - bgw) / 2);
                var width = (float)(bgw + 調整値);
                var height = (float)(Bitmap.Height + 調整値);
                Graphics.DrawImage(image, x, 0, width, height);
            }
            else
            {
                var width = (float)(Bitmap.Width + x比率 - 1);
                var height = (float)(Bitmap.Height + y比率 - 1);
                Graphics.DrawImage(image, 0, 0, width, height);
            }
        }

        private void FillImage(Image image)
        {
            double x比率 = Bitmap.Width / (double)image.Width;
            double y比率 = Bitmap.Height / (double)image.Height;
            if (x比率 > y比率) // 縦長の画像向け
            {
                double bgh = image.Height * x比率;
                double 調整値 = x比率 - 1;
                // 次にコメントアウトしている文では、1920×1199の画像の際にずれる。
                //Graphics.DrawImage(image, 0, (int)((Bitmap.Height - bgh) / 2 - 0.5), (float)(Bitmap.Width + 調整値), (float)(bgh + 調整値));
                var y = (int)((Bitmap.Height - (bgh + 調整値)) / 2);
                var width = (float)(Bitmap.Width + 調整値);
                var height = (float)(bgh + 調整値);
                Graphics.DrawImage(image, 0, y, width, height); // ずれを無くす実験中の文。
            }
            else if (x比率 < y比率) // 横長の画像向け
            {
                double bgw = image.Width * y比率;
                double 調整値 = y比率 - 1;
                //Graphics.DrawImage(image, (int)((Bitmap.Width - bgw) / 2 - 0.5), 0, (float)(bgw + 調整値), (float)(Bitmap.Height + 調整値));
                var x = (int)((Bitmap.Width - bgw) / 2);
                var width = (float)(bgw + 調整値);
                var height = (float)(Bitmap.Height + 調整値);
                Graphics.DrawImage(image, x, 0, width, height); // ずれを無くす実験中の文。
            }
            else
            {
                var width = (float)(Bitmap.Width + x比率 - 1);
                var height = (float)(Bitmap.Height + y比率 - 1);
                Graphics.DrawImage(image, 0, 0, width, height);
            }
        }
    }
}