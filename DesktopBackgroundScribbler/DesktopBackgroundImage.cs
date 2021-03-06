﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace DesktopBackgroundScribbler
{
    internal class DesktopBackgroundImage : IDisposable
    {
        private Bitmap bitmap;

        public Graphics Graphics { get; private set; }

        public string Original { get; private set; }

        public DesktopBackgroundImage(int width, int height)
        {
            bitmap = new Bitmap(width, height);
            Graphics = Graphics.FromImage(bitmap);

            Graphics.SmoothingMode = SmoothingMode.HighQuality;

            Clear();
            DrawWallpaper();
        }

        private void Clear()
        {
            // 背景色で初期化する。
            // デスクトップの背景の画像が「ページ幅に合わせる」などの場合、背景色で初期化する必要は今のところ無いが、
            // 透過を含む画像が将来的に対応される可能性を考慮した。
            var rgb = GetBackgroundRgb().Take(3).ToArray();
            Graphics.Clear(Color.FromArgb(rgb[0], rgb[1], rgb[2]));
        }

        private IEnumerable<int> GetBackgroundRgb()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Colors"))
            {
                var backgroundParts = Convert.ToString(key.GetValue("Background"))
                    .Split(default(char[]), StringSplitOptions.RemoveEmptyEntries);
                foreach (var backgroundPart in backgroundParts)
                {
                    if (int.TryParse(backgroundPart, out int i))
                    {
                        yield return i < 0 ? 0 : i > 255 ? 255 : i; // i が0未満なら0、255超なら255を返す。
                    }
                }
            }

            while (true)
            {
                yield return 0;
            }
        }

        private void DrawWallpaper()
        {
            using (var key = GetDesktopKey())
            {
                Original = GetWallpaperValue(key);
                if (!File.Exists(Original))
                {
                    return;
                }

                // Wallpaper に不正な画像ファイルが登録されていた場合に備えて try 節で囲む。
                // その場合は単に return する。
                try
                {
                    // Wallpaper のパスは元画像ではなく、トランスコードされた画像の可能性がある。
                    // スライドショー設定にしている場合など。
                    // 厳密には、HKEY_CURRENT_USER\Software\Microsoft\Internet Explorer\Desktop\General などを調べることで、
                    // 元画像からデスクトップの背景を再現できる。
                    // 参考 : http://detail.chiebukuro.yahoo.co.jp/qa/question_detail/q1064110280
                    using (var wallpaperBitmap = new Bitmap(Original))
                    {
                        // (TileWallpaper, WallpaperStyle)
                        // ページ幅に合わせる　　: (0, 10)
                        // 画面のサイズに合わせる: (0,  6)
                        // 拡大して表示　　　　　: (0,  2)
                        // 並べて表示　　　　　　: (1,  0)
                        // 中央に表示　　　　　　: (0,  0)
                        // スパン　　　　　　　　: (0, 22)
                        // 参考: https://smdn.jp/programming/tips/setdeskwallpaper/
                        if (wallpaperBitmap.Width == bitmap.Width && wallpaperBitmap.Height == bitmap.Height)
                        {
                            // 現在のデスクトップの背景画像のサイズが、作成する画像サイズと同じならば、
                            // レジストリの読み取りなどを省略できる。
                            // 連続で Scribble する場合はこのブロックを通ることが多いはず。
                            // なお、wallpaperBitmap をそのまま Bitmap プロパティに設定してはいけない。
                            // wallpaperBitmap はファイルから作成した Bitmap なので、Dispose() しなければならない。
                            PutImage(wallpaperBitmap);
                            return;
                        }

                        if (int.TryParse(Convert.ToString(key.GetValue("TileWallpaper")), out int tile) && tile == 1)
                        {
                            TileImage(wallpaperBitmap);
                            return;
                        }

                        if (!int.TryParse(Convert.ToString(key.GetValue("WallpaperStyle")), out int style))
                        {
                            style = 10; // ページ横幅に合わせる
                        }

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
                catch { }
            }
        }

        private static RegistryKey GetDesktopKey()
        {
            return Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
        }

        private static string GetWallpaperValue(RegistryKey key)
        {
            return Convert.ToString(key.GetValue("Wallpaper"));
        }

        private void PutImage(Image image)
        {
            Graphics.DrawImage(image, 0, 0, image.Width, image.Height);
        }

        private void TileImage(Image image)
        {
            for (int y = 0; y < bitmap.Height; y += image.Height)
            {
                for (int x = 0; x < bitmap.Width; x += image.Width)
                    Graphics.DrawImage(image, x, y, image.Width, image.Height);
            }
        }

        private void CenterImage(Image image)
        {
            var x = (bitmap.Width - image.Width) / 2;
            var y = (bitmap.Height - image.Height) / 2;
            Graphics.DrawImage(image, x, y, image.Width, image.Height);
        }

        private void StretchImage(Image image)
        {
            // 少々強引だが、幅と高さをこのように調整すれば過不足無くずれを吸収できる。
            var width = bitmap.Width + bitmap.Width / (float)image.Width - 1;
            var height = bitmap.Height + bitmap.Height / (float)image.Height - 1;
            Graphics.DrawImage(image, 0, 0, width, height);
        }

        private void FitImage(Image image)
        {
            double x比率 = bitmap.Width / (double)image.Width;
            double y比率 = bitmap.Height / (double)image.Height;
            if (x比率 < y比率) // 横長の画像向け。
            {
                double bgh = image.Height * x比率;
                double 調整値 = x比率 - 1;
                var y = (int)((bitmap.Height - bgh) / 2);
                var width = (float)(bitmap.Width + 調整値);
                var height = (float)(bgh + 調整値);
                Graphics.DrawImage(image, 0, y, width, height);
            }
            else if (x比率 > y比率) // 縦長の画像向け。
            {
                double bgw = image.Width * y比率;
                double 調整値 = y比率 - 1;
                var x = (int)((bitmap.Width - bgw) / 2);
                var width = (float)(bgw + 調整値);
                var height = (float)(bitmap.Height + 調整値);
                Graphics.DrawImage(image, x, 0, width, height);
            }
            else
            {
                var width = (float)(bitmap.Width + x比率 - 1);
                var height = (float)(bitmap.Height + y比率 - 1);
                Graphics.DrawImage(image, 0, 0, width, height);
            }
        }

        private void FillImage(Image image)
        {
            double x比率 = bitmap.Width / (double)image.Width;
            double y比率 = bitmap.Height / (double)image.Height;
            if (x比率 > y比率) // 縦長の画像向け
            {
                double bgh = image.Height * x比率;
                double 調整値 = x比率 - 1;
                // 次にコメントアウトしている文では、1920×1199の画像の際にずれる。
                //Graphics.DrawImage(image, 0, (int)((Bitmap.Height - bgh) / 2 - 0.5), (float)(Bitmap.Width + 調整値), (float)(bgh + 調整値));
                var y = (int)((bitmap.Height - (bgh + 調整値)) / 2);
                var width = (float)(bitmap.Width + 調整値);
                var height = (float)(bgh + 調整値);
                Graphics.DrawImage(image, 0, y, width, height); // ずれを無くす実験中の文。
            }
            else if (x比率 < y比率) // 横長の画像向け
            {
                double bgw = image.Width * y比率;
                double 調整値 = y比率 - 1;
                //Graphics.DrawImage(image, (int)((Bitmap.Width - bgw) / 2 - 0.5), 0, (float)(bgw + 調整値), (float)(Bitmap.Height + 調整値));
                var x = (int)((bitmap.Width - bgw) / 2);
                var width = (float)(bgw + 調整値);
                var height = (float)(bitmap.Height + 調整値);
                Graphics.DrawImage(image, x, 0, width, height); // ずれを無くす実験中の文。
            }
            else
            {
                var width = (float)(bitmap.Width + x比率 - 1);
                var height = (float)(bitmap.Height + y比率 - 1);
                Graphics.DrawImage(image, 0, 0, width, height);
            }
        }

        internal void Save(string filePath)
        {
            bitmap.Save(filePath, ImageFormat.Bmp);
        }

        internal static string GetCurrentPath()
        {
            using (var key = GetDesktopKey())
            {
                return GetWallpaperValue(key);
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // 重複する呼び出しを検出するには

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: マネージ状態を破棄します (マネージ オブジェクト)。
                }

                // TODO: アンマネージ リソース (アンマネージ オブジェクト) を解放し、下のファイナライザーをオーバーライドします。
                Graphics.Dispose();
                bitmap.Dispose();
                // TODO: 大きなフィールドを null に設定します。
                Graphics = null;
                bitmap = null;

                disposedValue = true;
            }
        }

        // TODO: 上の Dispose(bool disposing) にアンマネージ リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします。
        ~DesktopBackgroundImage()
        {
            // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
            Dispose(false);
        }

        // このコードは、破棄可能なパターンを正しく実装できるように追加されました。
        public void Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
            Dispose(true);
            // TODO: 上のファイナライザーがオーバーライドされる場合は、次の行のコメントを解除してください。
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}