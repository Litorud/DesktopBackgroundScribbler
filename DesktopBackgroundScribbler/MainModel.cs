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

        readonly string backupDirectory = Path.GetFullPath(@"Backup");

        // デスクトップの背景に設定する画像はカレントディレクトリに保存する方針だが、
        // レジストリの登録は絶対パスでなければならないので、この時点で絶対パスに変換しておく。
        // たとえばFile.Exists()やFile.Move()など、メソッドの内部で絶対パスを取得する処理をしているので、問題無いなら最初から絶対パスを渡したほうがよいと判断。
        // ちなみに、プログラムの実行中にファイルの移動はおそらくできないはずなので、絶対パスを保持するようにしても問題無いはず。
        readonly string filePath = Path.GetFullPath(@"Background.bmp");

        Scribbler scribbler = new Scribbler();

        int undoCount = 0;

        History history = new History();

        public void Scribble(string text)
        {
            // デスクトップの背景となる画像のサイズを決定する。
            var imageBounds = Screen.PrimaryScreen.Bounds;

            // 描画
            using (var image = new DesktopBackgroundImage(imageBounds.Width, imageBounds.Height))
            {
                scribbler.Scribble(text, image.Graphics, imageBounds.Width, imageBounds.Height);

                BackupImage();
                image.Save(filePath);
            }

            // デスクトップの背景に設定
            SetBackgroundImage(filePath);

            // 履歴
            history.Push(text);
        }

        private void BackupImage()
        {
            switch (undoCount)
            {
                case 0:
                    var newFileName = DateTime.Now.ToString("yyyyMMddTHHmmss") + Path.GetExtension(filePath);
                    var destPath = Path.Combine(backupDirectory, newFileName);

                    Directory.CreateDirectory(backupDirectory);
                    File.Copy(filePath, destPath);

                    // 古い画像を削除する。
                    var query = Directory.EnumerateFiles(backupDirectory)
                        .OrderByDescending(f => Path.GetFileName(f))
                        .Skip(10);
                    foreach (var f in query)
                    {
                        File.Delete(f);
                    }
                    break;
                case 1:
                    return;
                default:
                    if (Directory.Exists(backupDirectory))
                    {
                        var query2 = Directory.EnumerateFiles(backupDirectory)
                            .OrderByDescending(f => f)
                            .Take(undoCount - 1);
                        foreach (var file in query2)
                        {
                            File.Delete(file);
                        }
                    }
                    undoCount = 0;
                    break;
            }
        }

        private void SetBackgroundImage(string filePath)
        {
            // 第4引数の1は設定を更新するということ。なお、もし問題があったら第4引数を1 | 2にするとよいかもしれない。
            // 第4引数に2も指定すると、設定の更新を全てのアプリケーションに通知する。
            SystemParametersInfo(20, 0, filePath, 1);
        }

        internal void Undo()
        {
            // 現在の背景のパスを取得する。
            var currentPath = DesktopBackgroundImage.GetCurrentPath();

            // 現在の背景のパスが存在しない場合、それは有効なパスではない可能性がある。
            // 有効かもしれないが、簡単には判別できない。
            // そこで、このような場合はこのプログラムが管理する最新の背景を設定する。
            // 最新の背景も存在しない場合は何もしない。
            if (!File.Exists(currentPath))
            {
                if (File.Exists(filePath))
                {
                    SetBackgroundImage(filePath);
                }
                return;
            }

            var currentFullPath = Path.GetFullPath(currentPath);

            if (currentFullPath == filePath)
            {
                // 現在の背景のパスが Background.bmp ならば、Backup 内の最新の画像を設定する。
                // Backup 内に画像が一切無い場合は何もしない。
                if (!Directory.Exists(backupDirectory))
                {
                    return;
                }

                var path = Directory.EnumerateFiles(backupDirectory).OrderByDescending(f => Path.GetFileName(f)).FirstOrDefault();
                if (path != null)
                {
                    SetBackgroundImage(path);
                    undoCount++;
                }
            }
            else if (Path.GetDirectoryName(currentFullPath) == backupDirectory)
            {
                // 現在の背景のパスが Backup 内の画像を指しているならば、その画像より一つ古い画像を設定する。
                // そのような画像が無ければ何もしない。
                if (!Directory.Exists(backupDirectory))
                {
                    return;
                }

                var currentFileName = Path.GetFileName(currentPath);
                var file = Directory.EnumerateFiles(backupDirectory)
                    .Select(f => new { Name = Path.GetFileName(f), Path = f })
                    .Where(f => f.Name.CompareTo(currentFileName) < 0)
                    .OrderByDescending(f => f.Name)
                    .FirstOrDefault();
                if (file != null)
                {
                    SetBackgroundImage(file.Path);
                    undoCount++;
                }
            }
            else
            {
                // 現在の背景のパスは存在するが、それはこのプログラムによって設定したものではない場合。
                // Background.bmp を設定する。
                if (File.Exists(filePath))
                {
                    SetBackgroundImage(filePath);
                }
            }
        }

        internal void Redo()
        {
            // 現在の背景のパスを取得する。
            var currentPath = DesktopBackgroundImage.GetCurrentPath();

            // 現在の背景のパスが Background.bmp ならば、すでに最新の背景が設定されており、やり直す先は無い。
            // そのため、何もしない。
            if (currentPath == filePath)
            {
                return;
            }

            // 現在の背景のパスが存在しない場合、それはこのプログラムとは関係ない操作で設定されたパスである。
            // 「やり直し」はあくまで、「元に戻す」操作に対して「やり直し」するものなので、
            // プログラムとは関係ない操作に対して「やり直し」はできない。
            // したがって、何もしない。
            if (!File.Exists(currentPath))
            {
                return;
            }

            var currentFullPath = Path.GetFullPath(currentPath);

            if (Path.GetDirectoryName(currentFullPath) == backupDirectory)
            {
                // 現在の背景のパスが Backup 内の画像を指しているならば、その画像より一つ新しい画像を設定する。
                // そのような画像が無ければ、Background.bmp を設定する。
                // Backup が存在しなければ、Backup 内の一つ新しい画像もあるわけがないので、Background.bmp を設定する。
                if (!Directory.Exists(backupDirectory))
                {
                    SetBackgroundImage(filePath);
                    undoCount--;
                    return;
                }

                var currentFileName = Path.GetFileName(currentPath);
                var file = Directory.EnumerateFiles(backupDirectory)
                    .Select(f => new { Name = Path.GetFileName(f), Path = f })
                    .Where(f => f.Name.CompareTo(currentFileName) > 0)
                    .OrderBy(f => f.Name)
                    .FirstOrDefault();
                if (file == null)
                {
                    SetBackgroundImage(filePath);
                    undoCount--;
                }
                else
                {
                    SetBackgroundImage(file.Path);
                    undoCount--;
                }
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
    }
}
