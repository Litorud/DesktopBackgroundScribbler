using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DesktopBackgroundScribbler
{
    public class MainModel
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        // たとえばFile.Exists()やFile.Move()など、メソッドの内部で絶対パスを取得する処理をしているので、問題無いなら最初から絶対パスを渡したほうがよいと判断。
        // ちなみに、プログラムの実行中にファイルの移動はおそらくできないはずなので、絶対パスを保持するようにしても問題無いはず。
        readonly string filePath = Path.GetFullPath("Background.bmp");

        readonly int WIDTH;
        readonly int HEIGHT;

        readonly FontFamily[] generalFontFamilies = new FontFamily[2];
        readonly FontFamily[] asciiFontFamilies = new FontFamily[4];
        const int BOLD = (int)System.Drawing.FontStyle.Bold;
        const int BOLDITALIC = BOLD | (int)System.Drawing.FontStyle.Italic;

        Random random = new Random();

        Bitmap bitmap;
        Graphics graphics;

        // 履歴関係
        readonly string HISTORY_DIRECTORY = Path.GetFullPath("History");
        readonly string HISTORY_PATH;

        // 画像の履歴関係
        const int IMAGE_HISTORY_SIZE = 5;
        const int IMAGE_MAX_INDEX = IMAGE_HISTORY_SIZE - 1;
        string[] imageHistory = new string[IMAGE_HISTORY_SIZE];
        // 次に最新の元に戻す履歴が記録される添え字。
        // 基本的に一番古い履歴が記録されている添え字であり、もしくはまだ記録されていない添え字のこともある。
        int imageLeadIndex;

        int imageFocusIndex; // 元に戻す操作を行ったとき、どの履歴が背景になっているかを表す添え字。
        int imageCount; // 今存在する元に戻す履歴の数。

        int undoCount = 0; // 何回元に戻す操作を行ったかを表す。文字列履歴と違い、余分データを持たせないので、focus変数だけでなくこの変数が必要。

        // 文字列の履歴関係
        const char STRING_SEPARATOR = '\n';
        const int STRING_HISTORY_SIZE = 10;
        const int STRING_HISTORY_LENGTH = STRING_HISTORY_SIZE + 1; // ダミーとして、存在するHistoryEntryの数は常に保存できる履歴の数より一つ多い。
        string[] stringHistory;
        int stringLeadIndex; // 次に更新される履歴（=最も古い履歴）を表す。
        int stringFocusIndex;
        int stringCount; // 保持している履歴の数を表す。したがって、STRING_HISTORY_SIZEと等しい数を最大とする。これは、stringHistoryにおける最大の添え字も意味する。
        delegate void StringHistoryUpdate(string text);
        StringHistoryUpdate UpdateStringHistory;

        public MainModel()
        {
            Rectangle rectangle = Screen.PrimaryScreen.Bounds;
            WIDTH = rectangle.Width;
            HEIGHT = rectangle.Height;

            //
            // フォントの準備
            //

            try
            {
                generalFontFamilies[0] = new FontFamily("ＭＳ Ｐ明朝");
            }
            catch (ArgumentException)
            {
                generalFontFamilies[0] = System.Drawing.FontFamily.GenericSerif;
            }
            try
            {
                generalFontFamilies[1] = new FontFamily("メイリオ");
            }
            catch (ArgumentException)
            {
                generalFontFamilies[1] = System.Drawing.FontFamily.GenericSansSerif;
            }

            // Segoe Scriptも使いたかったが、パスの重なった部分の塗りつぶし方が思うようにならなかったので諦め。
            try
            {
                asciiFontFamilies[0] = new FontFamily("Comic Sans MS");
            }
            catch (ArgumentException)
            {
                asciiFontFamilies[0] = System.Drawing.FontFamily.GenericSansSerif;
            }
            try
            {
                asciiFontFamilies[1] = new FontFamily("Georgia");
            }
            catch (ArgumentException)
            {
                asciiFontFamilies[1] = System.Drawing.FontFamily.GenericSerif;
            }
            try
            {
                asciiFontFamilies[2] = new FontFamily("Impact");
            }
            catch (ArgumentException)
            {
                asciiFontFamilies[2] = System.Drawing.FontFamily.GenericSansSerif;
            }
            try
            {
                asciiFontFamilies[3] = new FontFamily("Times New Roman");
            }
            catch (ArgumentException)
            {
                asciiFontFamilies[3] = System.Drawing.FontFamily.GenericSansSerif;
            }

            //
            // BitmapとGraphicsの準備
            //

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop"))
            {
                object keyValue = key.GetValue("Wallpaper");
                string wallpaper;
                if (keyValue == null || string.IsNullOrEmpty(wallpaper = keyValue as string))
                {
                    // レジストリにデスクトップの背景のパスが指定されていなかった場合。
                    // 背景色で初期化したGraphicsを作る。
                    InitializeGraphicsByBackgroundColor();
                }
                else if (wallpaper == filePath)
                {
                    // レジストリにデスクトップの背景のパスが指定されており、それがこのプログラムによるものである場合。
                    InitializeGraphicsBySelfImage();
                }
                else
                {
                    // 現在のデスクトップの背景がこのプログラムによるものではない場合。
                    // HKEY_CURRENT_USER\Software\Microsoft\Internet Explorer\Desktop\Generalを調べる。
                    // ここがFILE_PATHだったら、FILE_PATHを使ってGraphicsを作る。
                    // 参考 : http://detail.chiebukuro.yahoo.co.jp/qa/question_detail/q1064110280
                    using (RegistryKey key2 = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Internet Explorer\Desktop\General"))
                        keyValue = key2.GetValue("WallpaperSource");
                    if (keyValue == null)
                    {
                        // wallpaperを使って再現。
                        if (File.Exists(wallpaper))
                            InitializeGraphicsByOtherImage(wallpaper, key);
                        else
                            InitializeGraphicsByBackgroundColor();
                    }
                    else
                    {
                        string wallpaperSource = keyValue as string;
                        if (wallpaperSource == filePath)
                        {
                            // 現在のデスクトップの背景の元になった画像がこのプログラムによるものである場合。
                            // FILE_PATHを使って再現。
                            InitializeGraphicsBySelfImage();
                        }
                        else
                        {
                            // 現在のデスクトップの背景がこのプログラムによるものではなく、
                            // 現在のデスクトップの背景の元になった画像もこのプログラムによるものではない場合。
                            // wallpaperSourceまたはwallpaperを使って再現。
                            if (File.Exists(wallpaperSource))
                                InitializeGraphicsByOtherImage(wallpaperSource, key);
                            else if (File.Exists(wallpaper))
                                InitializeGraphicsByOtherImage(wallpaper, key);
                            else
                                InitializeGraphicsByBackgroundColor();
                        }
                    }
                }
            }

            graphics.SmoothingMode = SmoothingMode.HighQuality;

            //
            // 履歴関係
            //

            HISTORY_PATH = Path.Combine(HISTORY_DIRECTORY, "History.bin");

            if (File.Exists(HISTORY_PATH))
            {
                byte[] bytes = File.ReadAllBytes(HISTORY_PATH);

                // 画像の履歴関係
                imageLeadIndex = bytes[0];
                imageCount = bytes[1];

                imageFocusIndex = imageLeadIndex;

                // 文字列の履歴関係
                stringHistory = Encoding.UTF8.GetString(bytes, 2, bytes.Length - 2).Split(STRING_SEPARATOR);

                stringCount = stringHistory.Length;

                if (stringCount > STRING_HISTORY_SIZE)
                {
                    stringCount = STRING_HISTORY_SIZE;
                    UpdateStringHistory = ModifyHistory;
                }
                else if (stringCount == STRING_HISTORY_SIZE)
                {
                    UpdateStringHistory = ModifyHistory;
                }
                else
                {
                    UpdateStringHistory = AddHistory;
                }

                Array.Resize(ref stringHistory, STRING_HISTORY_LENGTH);

                stringLeadIndex = stringFocusIndex = stringCount;
            }
            else
            {
                // 画像の履歴関係
                imageLeadIndex = imageFocusIndex = imageCount = 0;

                // 文字列の履歴関係
                stringHistory = new string[STRING_HISTORY_LENGTH];
                UpdateStringHistory = AddHistory;
                stringLeadIndex = stringFocusIndex = stringCount = 0;
            }

            // 画像の履歴関係
            for (int i = 0; i < IMAGE_HISTORY_SIZE; i++)
                imageHistory[i] = Path.Combine(HISTORY_DIRECTORY, i + ".bmp");
        }

        /// <summary>
        /// フィールドbitmapとフィールドgraphicsを初期化します。
        /// WIDTH×HEIGHTの大きさで、レジストリ設定の背景色でClearします。
        /// </summary>
        private void InitializeGraphicsByBackgroundColor()
        {
            bitmap = new Bitmap(WIDTH, HEIGHT);
            graphics = Graphics.FromImage(bitmap);

            object keyValue;
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Colors"))
                keyValue = key.GetValue("Background");

            if (keyValue == null)
            {
                graphics.Clear(Color.White);
            }
            else
            {
                string[] rgbStr = (keyValue as string).Split(' ');
                int[] rgb = new int[3];
                int minLength = Math.Min(rgbStr.Length, rgb.Length);
                int i = 0;
                for (; i < minLength; i++)
                {
                    if (!int.TryParse(rgbStr[i], out rgb[i]))
                        rgb[i] = 0;
                }
                for (; i < rgb.Length; i++)
                    rgb[i] = 0;

                graphics.Clear(Color.FromArgb(rgb[0], rgb[1], rgb[2]));
            }
        }

        /// <summary>
        /// Background.bmpで、フィールドbitmapとフィールドgraphicsを初期化します。
        /// Background.bmpが存在しない場合、InitializeGraphicsByBackgroundColor()を呼び出します。
        /// </summary>
        private void InitializeGraphicsBySelfImage()
        {
            if (File.Exists(filePath))
            {
                Bitmap source = new Bitmap(filePath);
                bitmap = new Bitmap(source);
                source.Dispose();
                graphics = Graphics.FromImage(bitmap);
            }
            else
            {
                InitializeGraphicsByBackgroundColor();
            }
        }

        /// <summary>
        /// 指定されたファイル パスで、フィールドbitmapとフィールドgraphicsを初期化します。
        /// 指定されたファイル パスが有効かどうかの検査はしません。
        /// 再現にあたって、レジストリの値を取得するので、そのためのRegistryKeyを渡す必要があります。
        /// </summary>
        /// <param name="path">初期化に使う画像ファイルのパス。</param>
        /// <param name="key">WallpaperStyleとTileWallpaperを取得するためのRegistryKey。</param>
        private void InitializeGraphicsByOtherImage(string path, RegistryKey key)
        {
            // ところで、ここで履歴で、一番初めの描画より前に戻れるようにする処理を初期化するといいと思う。
            // 前の画像があるフラグを5にする。
            //  * 無ければ0になってる。
            // 前の画像があるフラグが1以上なら、描き込まれるたびに、フラグを減算する。
            // 元に戻す操作の際、これ以上戻れなくなったら、前の画像があるフラグを調べる。
            // 前の画像があるフラグが1以上なら、前の画像を背景に設定し、前の画像を背景に設定したフラグを立てる。
            // やり直しの際は、前の画像を背景に設定したフラグを調べ、立っていたら折り、一番古い画像を背景に設定する。
            // 前の画像に戻った状態で描き込んだら、前の画像を背景に設定したフラグを折り、前の画像があるフラグを4にする。

            // 背景色で初期化したGraphicsを作る。
            // デスクトップの背景を再現する場合は、並べて表示、画面に合わせて伸縮、ページ横幅に合わせるについては、
            // Graphicsを背景色でClearする必要は今のところ無いが、将来的には透過を含む画像がデスクトップの背景に指定できるかも知れず、
            // そのときには背景色でClearしておく必要があるし、またプログラムも単純になるので、
            // 常にここで背景色でClearすることにする。
            InitializeGraphicsByBackgroundColor();

            // http://smdn.jp/programming/tips/setdeskwallpaper/ を参考に。
            Bitmap original = new Bitmap(path);
            int w = original.Width;
            int h = original.Height;

            int style, tile;
            object keyValue = key.GetValue("WallpaperStyle");
            if (keyValue == null || !int.TryParse(keyValue as string, out style))
                style = 10;
            keyValue = key.GetValue("TileWallpaper");
            if (keyValue == null || !int.TryParse(keyValue as string, out tile))
                tile = 0;

            if (style == 0 && tile == 0) // 中央に表示
            {
                graphics.DrawImage(original, (WIDTH - w) / 2, (HEIGHT - h) / 2, w, h);
            }
            else if (style == 0 && tile == 1) // 並べて表示
            {
                for (int y = 0; y < HEIGHT; y += h)
                {
                    for (int x = 0; x < WIDTH; x += w)
                        graphics.DrawImage(original, x, y, w, h);
                }
            }
            else if (style == 2 && tile == 0) // 画面に合わせて伸縮
            {
                // 少々強引だが、幅と高さをこのように調整すれば過不足無くずれを吸収できる。
                graphics.DrawImage(original, 0, 0, WIDTH + WIDTH / (float)w - 1, HEIGHT + HEIGHT / (float)h - 1);
            }
            else if (style == 6 && tile == 0) // ページ縦幅に合わせる
            {
                double x比率 = WIDTH / (double)w;
                double y比率 = HEIGHT / (double)h;
                if (x比率 < y比率) // 横長の画像向け。
                {
                    double bgh = original.Height * x比率;
                    double 調整値 = x比率 - 1;
                    graphics.DrawImage(original, 0, (int)((HEIGHT - bgh) / 2), (float)(WIDTH + 調整値), (float)(bgh + 調整値));
                }
                else if (x比率 > y比率) // 縦長の画像向け。
                {
                    double bgw = original.Width * y比率;
                    double 調整値 = y比率 - 1;
                    graphics.DrawImage(original, (int)((WIDTH - bgw) / 2), 0, (float)(bgw + 調整値), (float)(HEIGHT + 調整値));
                }
                else
                {
                    graphics.DrawImage(original, 0, 0, (float)(WIDTH + x比率 - 1), (float)(HEIGHT + y比率 - 1));
                }
            }
            else // ページ横幅に合わせる
            {
                double x比率 = WIDTH / (double)w;
                double y比率 = HEIGHT / (double)h;
                if (x比率 > y比率) // 縦長の画像向け
                {
                    double bgh = original.Height * x比率;
                    double 調整値 = x比率 - 1;
                    // 次にコメントアウトしている文では、1920×1199の画像の際にずれる。
                    //graphics.DrawImage(original, 0, (int)((HEIGHT - bgh) / 2 - 0.5), (float)(WIDTH + 調整値), (float)(bgh + 調整値));
                    graphics.DrawImage(original, 0, (int)((HEIGHT - (bgh + 調整値)) / 2), (float)(WIDTH + 調整値), (float)(bgh + 調整値)); // ずれを無くす実験中の文。
                }
                else if (x比率 < y比率) // 横長の画像向け
                {
                    double bgw = original.Width * y比率;
                    double 調整値 = y比率 - 1;
                    //graphics.DrawImage(original, (int)((WIDTH - bgw) / 2 - 0.5), 0, (float)(bgw + 調整値), (float)(HEIGHT + 調整値));
                    graphics.DrawImage(original, (int)((WIDTH - bgw) / 2), 0, (float)(bgw + 調整値), (float)(HEIGHT + 調整値)); // ずれを無くす実験中の文。
                }
                else
                {
                    graphics.DrawImage(original, 0, 0, (float)(WIDTH + x比率 - 1), (float)(HEIGHT + y比率 - 1));
                }
            }

            original.Dispose();
        }

        //
        // 文字列の履歴関係
        //

        private void ModifyHistory(string text)
        {
            stringHistory[stringLeadIndex] = text;

            IncrementStringIndex(ref stringLeadIndex);
        }

        private void AddHistory(string text)
        {
            stringHistory[stringLeadIndex] = text;
            stringLeadIndex++;

            if (++stringCount == STRING_HISTORY_SIZE)
                UpdateStringHistory = ModifyHistory;
        }

        //
        // 履歴の添え字を増減させるためのメソッドたち。
        //

        void IncrementImageIndex(ref int i)
        {
            if (i >= IMAGE_MAX_INDEX)
                i = 0;
            else
                i++;
        }

        void DecrementImageIndex(ref int i)
        {
            if (i == 0)
                i = IMAGE_MAX_INDEX;
            else
                i--;
        }

        void IncrementStringIndex(ref int i)
        {
            if (i >= stringCount)
                i = 0;
            else
                i++;
        }

        void DecrementStringIndex(ref int i)
        {
            if (i == 0)
                i = stringCount;
            else
                i--;
        }

        public void Scribble(string text)
        {
            // フォントファミリーとフォントスタイルをランダムに決定する。
            var fontInfo = FontInfo.GenerateRandomFontInfo(text, random);

            // デスクトップの背景となる画像のサイズを決定する。
            var imageBounds = Screen.PrimaryScreen.Bounds;

            // 画像のサイズから、最小フォントサイズを短辺の1/100と決定する。また、最大フォントサイズを短辺の2倍と決定する。
            var 短辺 = Math.Min(imageBounds.Width, imageBounds.Height);
            var minFontSize = 短辺 / 100F;
            var maxFontSize = 短辺 * 2F;

            // フォントファミリー、フォントスタイル、最小フォントサイズから、テキストパスサイズを求める。
            var textPath = new TextPath(text, fontInfo, minFontSize);

            var pathBounds = textPath.Path.GetBounds();

            // 最小フォントサイズ、最大フォントサイズ、テキストパスサイズ、画像サイズから、拡大率を決定する。
            var scaleRatio = GenerateRandomScaleRatio(
                minFontSize, maxFontSize,
                pathBounds.Width, pathBounds.Height,
                imageBounds.Width, imageBounds.Height,
                random);

            // 傾きを決定する。
            var angle = GenerateRandomAngle(45, random);

            // 最小フォントサイズ、拡大率、テキストパスサイズ、画像サイズ、傾きから、位置を決定する。
            var point = GenerateRandomPoint(
                minFontSize,
                scaleRatio,
                pathBounds.Width, pathBounds.Height,
                imageBounds.Width, imageBounds.Height,
                angle,
                random);

            // 拡大率、傾き、位置に基づいて GraphicsPath を変換する。
            textPath.Transform(scaleRatio, angle, point);

            // 色を決定する。色に基づいて Brush を作成する。
            var color = GenerateRandomColor(random);
            var brush = new SolidBrush(color);

            // 色から、縁取りの色を求める。
            var strokeColor = color.GetBrightness() > 0.95F ? Color.Black : Color.White;

            // 拡大率から、縁取りの幅を決定する。
            var strokeWidth = GenerateRandomStrokeWidth(scaleRatio, random);

            // 縁取りの色、縁取りの幅に基づいて Pen を作成する。
            var pen = new Pen(strokeColor, (float)strokeWidth);

            // 描画
            using (var image = new DesktopBackgroundImage(imageBounds.Width, imageBounds.Height))
            {
                image.Draw(textPath.Path, brush, pen);
                image.Save(filePath);
            }

            // デスクトップの背景に設定
            SystemParametersInfo(20, 0, filePath, 1);
        }

        private double GenerateRandomScaleRatio(float minFontSize, float maxFontSize, float pathWidth, float pathHeight, int imageWidth, int imageHeight, Random random)
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

        private double GenerateRandomAngle(double maxAngle, Random random)
        {
            return random.NextDouble() * maxAngle * 2 - maxAngle;
        }

        private PointF GenerateRandomPoint(float minFontSize, double scaleRatio, float pathWidth, float pathHeight, int imageWidth, int imageHeight, double angle, Random random)
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

        private double GenerateRandomStrokeWidth(double scaleRatio, Random random)
        {
            // scaleRatio が1（=フォントサイズが10.8）のとき、1～3の範囲でランダムに幅を決めたい。
            // scaleRatio が100（フォントサイズが1080）のとき、10～50の範囲でランダムに幅を決めたい。
            var 振れ幅 = 38 * scaleRatio / 99 + 1.6;
            var オフセット = scaleRatio / 11 + 0.9;
            // 上記式の第2項は、毎回割り算が発生するコストが見合わないので、
            // 近似値のリテラルで置き換えている。

            return 振れ幅 * random.NextDouble() + オフセット;

            // 別の求め方として、許容する最小の幅と最大の幅を計算し、
            // (最大幅 - 最小幅) * random.NextDouble() + 最小幅 を返す方法もある。
        }

        private Color GenerateRandomColor(Random random)
        {
            var r = random.Next(256);
            var g = random.Next(256);
            var b = random.Next(256);
            return Color.FromArgb(r, g, b);
        }

        internal void Scribble_old(string text)
        {
            // 現背景を履歴に保存。
            if (Directory.Exists(HISTORY_DIRECTORY))
            {
                if (undoCount == 0)
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(imageHistory[imageLeadIndex]);
                        File.Move(filePath, imageHistory[imageLeadIndex]);
                        if (imageCount < IMAGE_HISTORY_SIZE)
                            imageCount++;

                        IncrementImageIndex(ref imageLeadIndex);

                        imageFocusIndex = imageLeadIndex;
                    }
                }
                else
                {
                    do
                    {
                        if (File.Exists(imageHistory[imageFocusIndex]))
                        {
                            graphics.Dispose();
                            bitmap.Dispose();

                            Bitmap source = new Bitmap(imageHistory[imageFocusIndex]);
                            bitmap = new Bitmap(source);
                            source.Dispose();

                            graphics = Graphics.FromImage(bitmap);
                            graphics.SmoothingMode = SmoothingMode.HighQuality;

                            IncrementImageIndex(ref imageFocusIndex);

                            int index = imageFocusIndex;

                            while (imageFocusIndex != imageLeadIndex)
                            {
                                File.Delete(imageHistory[imageFocusIndex]);

                                IncrementImageIndex(ref imageFocusIndex);
                            }
                            imageCount -= undoCount - 1;

                            imageLeadIndex = imageFocusIndex = index;
                            undoCount = 0;

                            break;
                        }

                        IncrementImageIndex(ref imageFocusIndex);
                        undoCount--;
                    } while (undoCount > 0);
                }
            }
            else
            {
                Directory.CreateDirectory(HISTORY_DIRECTORY);

                if (undoCount == 0)
                {
                    if (File.Exists(filePath))
                    {
                        File.Move(filePath, imageHistory[0]);
                        imageLeadIndex = imageFocusIndex = imageCount = 1;
                    }
                    else
                    {
                        imageLeadIndex = imageFocusIndex = 0;
                    }
                }
                else
                {
                    imageLeadIndex = imageFocusIndex = 0;
                    undoCount = 0;
                }
            }

            //
            // 座標以外のパラメーターを決定する。
            //

            // フォントファミリーを決定。
            FontFamily[] families = asciiFontFamilies;
            foreach (char c in text.ToCharArray())
            {
                if (c > 255)
                {
                    families = generalFontFamilies;
                    break;
                }
            }
            FontFamily fontFamily = families[random.Next(families.Length)];

            // フォントスタイルを決定。
            int fontStyle = random.Next(5) == 0 ? BOLDITALIC : BOLD;

            // 文字サイズを決定。単位はピクセル。
            double emSize = random.NextDouble() * 70 + 30;

            // 傾きを決定。
            double angle = random.NextDouble() * 20 - 10;

            // 色を決定。
            int r = random.Next(256);
            int g = random.Next(256);
            int b = random.Next(256);
            Color color = Color.FromArgb(r, g, b);

            // 縁取りのペンを決定。
            Pen pen = new Pen(r >= 224 && g >= 224 && b >= 224 ? Color.Black : Color.White, (float)Math.Max(emSize / 20, 3));

            //
            // 描画
            //

            GraphicsPath gp = new GraphicsPath();
            gp.AddString(text, fontFamily, fontStyle, (float)emSize, PointF.Empty, StringFormat.GenericDefault);

            // 文字列の中心を原点にし、その後原点を中心に回転させる。
            RectangleF bounds = gp.GetBounds();
            Matrix matrix = new Matrix();
            matrix.Rotate((float)angle);
            // 描画したのは(0, 0)だが、GetBoundsで得たXやYは0ではない。
            matrix.Translate(-bounds.X - bounds.Width / 2, -bounds.Y - bounds.Height / 2);
            gp.Transform(matrix);

            // 画像上において、文字列の中心となる座標を決定する。
            // 文字列の横方向は、文字サイズまでなら見切れてもいいものとする。
            // 文字列の縦方向は、文字サイズの半分までなら見切れてもいいものとする。
            double w = gp.GetBounds().Width;
            double x = (WIDTH + emSize * 2 - w) * random.NextDouble() - emSize + w / 2;
            double y = HEIGHT * random.NextDouble();

            // そこへ向かって平行移動する。
            matrix = new Matrix();
            matrix.Translate((float)x, (float)y);
            gp.Transform(matrix);

            // そして描画。
            graphics.DrawPath(pen, gp);
            graphics.FillPath(new SolidBrush(color), gp);

            bitmap.Save(filePath, ImageFormat.Bmp);

            // 背景変更。
            // 第4引数の1は設定を更新するということ。なお、もし問題があったら第4引数を1 | 2にするとよいかもしれない。
            // 第4引数に2も指定すると、設定の更新を全てのアプリケーションに通知する。
            SystemParametersInfo(20, 0, filePath, 1);

            // 文字列の履歴更新処理
            UpdateStringHistory(text);
            stringFocusIndex = stringLeadIndex; // focusを先頭に戻す。
        }

        internal void Undo()
        {
            if (undoCount < imageCount)
            {
                undoCount++;

                DecrementImageIndex(ref imageFocusIndex);

                if (File.Exists(imageHistory[imageFocusIndex]))
                    SystemParametersInfo(20, 0, imageHistory[imageFocusIndex], 1);
            }
        }

        internal void Redo()
        {
            if (undoCount == 1)
            {
                undoCount--;

                IncrementImageIndex(ref imageFocusIndex);

                if (File.Exists(filePath))
                    SystemParametersInfo(20, 0, filePath, 1);
            }
            else if (undoCount > 1)
            {
                undoCount--;

                IncrementImageIndex(ref imageFocusIndex);

                if (File.Exists(imageHistory[imageFocusIndex]))
                    SystemParametersInfo(20, 0, imageHistory[imageFocusIndex], 1);
            }
        }

        internal string GetHistory(int change)
        {
            if (change > 0)
            {
                if (stringFocusIndex != stringLeadIndex)
                {
                    IncrementStringIndex(ref stringFocusIndex);
                    if (stringFocusIndex == stringLeadIndex)
                        return string.Empty;
                    else
                        return stringHistory[stringFocusIndex];
                }
            }
            else if (change < 0)
            {
                int index = stringFocusIndex - 1;
                if (index < 0)
                    index = stringCount;
                if (index != stringLeadIndex)
                {
                    stringFocusIndex = index;
                    return stringHistory[stringFocusIndex];
                }
            }

            return null;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            // 画像の履歴関係

            if (undoCount > 0)
            {
                File.Delete(filePath);
                if (File.Exists(imageHistory[imageFocusIndex]))
                {
                    File.Copy(imageHistory[imageFocusIndex], filePath);
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop"))
                        key.SetValue("Wallpaper", filePath);
                }

                do
                {
                    File.Delete(imageHistory[imageLeadIndex]);

                    DecrementImageIndex(ref imageLeadIndex);
                } while (imageLeadIndex != imageFocusIndex);
            }

            byte[] bytes1 = new byte[2];
            bytes1[0] = (byte)imageLeadIndex;
            bytes1[1] = (byte)(imageCount - undoCount);

            // 文字列の履歴関係

            if (!Directory.Exists(HISTORY_DIRECTORY))
                Directory.CreateDirectory(HISTORY_DIRECTORY);

            // 先に書き込まれるものほど古いデータ。
            StringBuilder sb = new StringBuilder();

            // 以下では、leadIndex及びfocusIndexを通常とは別の意味で使いまわしている。
            if (stringCount < STRING_HISTORY_SIZE)
            {
                stringFocusIndex = 0;
            }
            else
            {
                if (stringLeadIndex >= stringCount)
                    stringFocusIndex = 0;
                else
                    stringFocusIndex = stringLeadIndex + 1;
            }

            DecrementStringIndex(ref stringLeadIndex);

            while (stringFocusIndex != stringLeadIndex)
            {
                sb.Append(stringHistory[stringFocusIndex]);
                sb.Append(STRING_SEPARATOR);
                IncrementStringIndex(ref stringFocusIndex);
            }

            sb.Append(stringHistory[stringFocusIndex]);

            // 出力。
            byte[] bytes2 = Encoding.UTF8.GetBytes(sb.ToString());

            byte[] bytes3 = new byte[bytes1.Length + bytes2.Length];
            Buffer.BlockCopy(bytes1, 0, bytes3, 0, bytes1.Length);
            Buffer.BlockCopy(bytes2, 0, bytes3, bytes1.Length, bytes2.Length);

            File.WriteAllBytes(HISTORY_PATH, bytes3);
        }
    }
}
