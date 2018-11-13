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

        History history = new History();

        public void Scribble(string text)
        {
            // デスクトップの背景となる画像のサイズを決定する。
            var imageBounds = Screen.PrimaryScreen.Bounds;

            // 描画
            using (var image = new DesktopBackgroundImage(imageBounds.Width, imageBounds.Height))
            {
                scribbler.Scribble(text, image.Graphics, imageBounds.Width, imageBounds.Height);

                Backup(image.Original);
                image.Save(filePath);
            }

            // デスクトップの背景に設定
            SetBackgroundImage(filePath);

            // 履歴
            history.Push(text);
        }

        private void Backup(string currentPath)
        {
            if (File.Exists(currentPath))
            {
                var fullPath = Path.GetFullPath(currentPath);
                // 現在の背景が Backup\* の場合、現在の背景より新しい Backup\* を削除する。
                if (Path.GetDirectoryName(fullPath) == backupDirectory)
                {
                    if (Directory.Exists(backupDirectory))
                    {
                        var name = Path.GetFileName(currentPath);
                        foreach (var f in Directory.EnumerateFiles(backupDirectory)
                            .Where(f => Path.GetFileName(f).CompareTo(name) > 0))
                        {
                            File.Delete(f);
                        }
                    }
                    return;
                }
            }

            // 現在の背景が Backup\* ではなく、Background.bmp が存在する場合、
            // Background.bmp をバックアップする。
            if (File.Exists(filePath))
            {
                var newFileName = DateTime.Now.ToString("yyyyMMddTHHmmss,fff") + Path.GetExtension(filePath);
                var destPath = Path.Combine(backupDirectory, newFileName);

                Directory.CreateDirectory(backupDirectory);
                File.Move(filePath, destPath);

                // 古い画像を削除する。
                foreach (var f in Directory.EnumerateFiles(backupDirectory)
                    .OrderByDescending(f => Path.GetFileName(f))
                    .Skip(10))
                {
                    File.Delete(f);
                }
            }
        }

        private void SetBackgroundImage(string filePath)
        {
            // 第4引数の1は設定を保存するということ。
            // 第4引数に2も指定すると、設定の更新を全てのアプリケーションに通知する。
            // これが無くて困ったことはないけど、問題となるアプリケーションもあるかもしれないので通知する。
            SystemParametersInfo(20, 0, filePath, 1 | 2);
        }

        public void Undo()
        {
            // 現在の背景のパスを取得する。
            var currentPath = DesktopBackgroundImage.GetCurrentPath();
            if (string.IsNullOrWhiteSpace(currentPath))
            {
                最新の画像を設定();
                return;
            }

            string currentFullPath;
            try
            {
                currentFullPath = Path.GetFullPath(currentPath);
            }
            catch
            {
                // GetFullPath で例外が発生するということは、レジストリに無効なパスが設定されていたということ。
                // この場合は、このプログラムが分かる最も新しい画像を設定する。
                最新の画像を設定();
                return;
            }

            if (currentFullPath == filePath)
            {
                // 現在の背景のパスが Background.bmp ならば、一つ古い画像を設定する。
                最新のバックアップを設定();
                return;
            }

            if (Path.GetDirectoryName(currentFullPath) == backupDirectory)
            {
                // 現在の背景のパスが Backup 内の画像を指しているならば、その画像より一つ古い画像を設定する。
                // そのような画像が無ければ何もしない。
                if (!Directory.Exists(backupDirectory))
                {
                    return;
                }

                var currentFileName = Path.GetFileName(currentPath);
                var files = Directory.EnumerateFiles(backupDirectory)
                    .Select(f => new { Name = Path.GetFileName(f), Path = f })
                    .Where(f => f.Name.CompareTo(currentFileName) < 0)
                    .OrderByDescending(f => f.Name);
                if (files.Any())
                {
                    SetBackgroundImage(Path.GetFullPath(files.First().Path));
                }
                return;
            }

            // ここに到達するということは、有効ではあるが全くあずかり知らぬパスが設定されているということ。
            最新の画像を設定();
        }

        private void 最新の画像を設定()
        {
            if (File.Exists(filePath))
            {
                SetBackgroundImage(filePath);
                return;
            }

            最新のバックアップを設定();
        }

        private void 最新のバックアップを設定()
        {
            if (!Directory.Exists(backupDirectory))
            {
                return;
            }

            var files = Directory.EnumerateFiles(backupDirectory);
            if (!files.Any())
            {
                return;
            }

            var first = files.OrderByDescending(f => f).First();
            SetBackgroundImage(Path.GetFullPath(first));
        }

        public void Redo()
        {
            // 現在の背景のパスを取得する。
            var currentPath = DesktopBackgroundImage.GetCurrentPath();

            // 現在の背景のパスが Background.bmp、または無効なパスならばやり直せない。
            if (currentPath == filePath || string.IsNullOrWhiteSpace(currentPath))
            {
                return;
            }

            string directoryName;
            try
            {
                directoryName = Path.GetDirectoryName(currentPath);
            }
            catch
            {
                // GetDirectoryName で例外が発生するということは、レジストリに無効なパスが設定されていたということ。
                // この場合はやり直せない。
                return;
            }

            // 現在の背景のパスが Backup 内の画像を指していなければ、やり直せない。
            if (directoryName != backupDirectory)
            {
                return;
            }

            // 現在の背景のパスが Backup 内の画像を指しているならば、その画像より一つ新しい画像を設定する。
            if (!Directory.Exists(backupDirectory))
            {
                if (File.Exists(filePath))
                {
                    SetBackgroundImage(filePath);
                }
                return;
            }

            var currentFileName = Path.GetFileName(currentPath);
            var files = Directory.EnumerateFiles(backupDirectory)
                .Select(f => new { Name = Path.GetFileName(f), Path = f })
                .Where(f => f.Name.CompareTo(currentFileName) > 0)
                .OrderBy(f => f.Name);
            if (files.Any())
            {
                SetBackgroundImage(Path.GetFullPath(files.First().Path));
            }
            else if (File.Exists(filePath))
            {
                SetBackgroundImage(filePath);
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
