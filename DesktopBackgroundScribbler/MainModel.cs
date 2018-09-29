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

        // デスクトップの背景に設定する画像はカレントディレクトリに保存する方針だが、
        // レジストリの登録は絶対パスでなければならないので、この時点で絶対パスに変換しておく。
        // たとえばFile.Exists()やFile.Move()など、メソッドの内部で絶対パスを取得する処理をしているので、問題無いなら最初から絶対パスを渡したほうがよいと判断。
        // ちなみに、プログラムの実行中にファイルの移動はおそらくできないはずなので、絶対パスを保持するようにしても問題無いはず。
        readonly string filePath = Path.GetFullPath("Background.bmp");

        Scribbler scribbler = new Scribbler();

        History history = new History();

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

        public MainModel()
        {
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
            }
            else
            {
                // 画像の履歴関係
                imageLeadIndex = imageFocusIndex = imageCount = 0;
            }

            // 画像の履歴関係
            for (int i = 0; i < IMAGE_HISTORY_SIZE; i++)
                imageHistory[i] = Path.Combine(HISTORY_DIRECTORY, i + ".bmp");
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

        public void Scribble(string text)
        {
            // デスクトップの背景となる画像のサイズを決定する。
            var imageBounds = Screen.PrimaryScreen.Bounds;

            // 描画
            using (var image = new DesktopBackgroundImage(imageBounds.Width, imageBounds.Height))
            {
                scribbler.Scribble(text, image.Graphics, imageBounds.Width, imageBounds.Height);
                image.Save(filePath);
            }

            // デスクトップの背景に設定
            // 第4引数の1は設定を更新するということ。なお、もし問題があったら第4引数を1 | 2にするとよいかもしれない。
            // 第4引数に2も指定すると、設定の更新を全てのアプリケーションに通知する。
            SystemParametersInfo(20, 0, filePath, 1);

            // 履歴
            history.Push(text);
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

        internal string ForwardHistory()
        {
            return history.ForwardHistory();
        }

        internal string BackHistory()
        {
            return history.BackHistory();
        }

        internal void SaveHistory()
        {
            history.Save();
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
            StringBuilder sb = new StringBuilder();

            // 出力。
            byte[] bytes2 = Encoding.UTF8.GetBytes(sb.ToString());

            byte[] bytes3 = new byte[bytes1.Length + bytes2.Length];
            Buffer.BlockCopy(bytes1, 0, bytes3, 0, bytes1.Length);
            Buffer.BlockCopy(bytes2, 0, bytes3, bytes1.Length, bytes2.Length);

            File.WriteAllBytes(HISTORY_PATH, bytes3);
        }
    }
}
