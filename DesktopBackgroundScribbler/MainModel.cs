using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DesktopBackgroundScribbler
{
    public class MainModel
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern bool SystemParametersInfo(uint uiAction, uint uiParam, string pvParam, uint fWinIni);

        readonly string currentDirectory;
        readonly string backupDirectory;

        Scribbler scribbler = new Scribbler();

        History history = new History();

        public MainModel()
        {
            currentDirectory = Directory.GetCurrentDirectory();
            backupDirectory = Path.Combine(currentDirectory, @"Backup");
        }

        public void Scribble(string text)
        {
            // デスクトップの背景となる画像のサイズを決定する。
            var imageWidth = (int)SystemParameters.PrimaryScreenWidth;
            var imageHeight = (int)SystemParameters.PrimaryScreenHeight;

            // ファイル名を決定する。
            // デスクトップの背景が「単色」に設定されている場合、SystemParametersInfo でファイルパスを指定しても、
            // それが以前設定していた画像パスと同じなら、画像の読み込みは行われず、既存の TranscodedWallpaper が使われ、
            // 結果として意図しない背景になることがある。
            // これに対応するため、日時付きのファイル名にして、同じ画像パスにならないようにする。
            // v1.1 では、常に Background.bmp というファイル名だったので、上記現象が発生していた。
            var fileName = "Background_" + DateTime.Now.ToString("yyyyMMddTHHmmss,fff", CultureInfo.InvariantCulture) + ".bmp";
            var filePath = Path.Combine(currentDirectory, fileName);

            // 描画
            using (var image = new DesktopBackgroundImage(imageWidth, imageHeight))
            {
                scribbler.Scribble(text, image.Graphics, imageWidth, imageHeight);

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
            // 以下の処理において、currentPath が指すファイルが実際に存在しているかどうかは関係ないのだが、
            // GetFullPath() で例外が発生するような文字列である可能性があるので、File.Exists() で検証している。
            if (File.Exists(currentPath))
            {
                var fullPath = Path.GetFullPath(currentPath);
                // 現在の背景が Backup\* の場合、現在の背景より新しい Backup\* と、カレントディレクトリの Background_yyyyMMddTHHmmss,fff.bmp を削除する。
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

                    foreach (var f in GetCurrentDirectoryFiles())
                    {
                        File.Delete(f);
                    }

                    return;
                }
            }

            // 現在の背景が Backup\* ではなく、Background_yyyyMMddTHHmmss,fff.bmp が存在する場合、
            // そのファイルをバックアップする。
            var currentDirectoryFiles = GetCurrentDirectoryFiles();
            if (currentDirectoryFiles.Any())
            {
                Directory.CreateDirectory(backupDirectory);
                foreach (var f in currentDirectoryFiles)
                {
                    var fileName = Path.GetFileName(f);
                    var destPath = Path.Combine(backupDirectory, fileName);

                    File.Move(f, destPath);
                }

                // 古い画像を削除する。
                foreach (var f in Directory.EnumerateFiles(backupDirectory)
                    .OrderByDescending(f => Path.GetFileName(f))
                    .Skip(10))
                {
                    File.Delete(f);
                }
            }
        }

        private IEnumerable<string> GetCurrentDirectoryFiles()
        {
            return Directory.EnumerateFiles(currentDirectory, "Background_*.bmp");
        }

        private void SetBackgroundImage(string filePath)
        {
            // 第4引数の1は設定を保存するということ。
            // 第4引数に2も指定すると、設定の更新を全てのアプリケーションに通知する。
            // これが無くて困ったことはないけど、問題となるアプリケーションもあるかもしれないので通知する。
            SystemParametersInfo(20U, 0U, filePath, 1U | 2U);
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

            var directoryName = Path.GetDirectoryName(currentFullPath);
            if (directoryName == currentDirectory)
            {
                // 現在の背景のパスがカレントディレクトリの画像を指しているならば、一つ古い画像を設定する。
                最新のバックアップを設定();
                return;
            }

            if (directoryName == backupDirectory)
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
            var files = GetCurrentDirectoryFiles();
            if (files.Any())
            {
                var first = files.OrderByDescending(f => Path.GetFileName(f)).First();
                SetBackgroundImage(Path.GetFullPath(first));
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

            var first = files.OrderByDescending(f => Path.GetFileName(f)).First();
            SetBackgroundImage(Path.GetFullPath(first));
        }

        public void Redo()
        {
            // 現在の背景のパスを取得する。
            var currentPath = DesktopBackgroundImage.GetCurrentPath();

            // 現在の背景のパスが無効なパスならばやり直せない。
            string currentFullPath;
            try
            {
                currentFullPath = Path.GetFullPath(currentPath);
            }
            catch
            {
                // GetFullPath で例外が発生するということは、レジストリに無効なパスが設定されていたということ。
                // この場合は、やり直せない。
                return;
            }

            // 現在の背景のパスが Backup 内の画像を指していなければ、やり直せない。
            var directoryName = Path.GetDirectoryName(currentPath);
            if (directoryName != backupDirectory)
            {
                return;
            }

            // 現在の背景のパスが Backup 内の画像を指しているならば、その画像より一つ新しい画像を設定する。
            if (!Directory.Exists(backupDirectory))
            {
                カレントディレクトリの画像を設定();
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
            else
            {
                カレントディレクトリの画像を設定();
            }
        }

        private void カレントディレクトリの画像を設定()
        {
            var files = GetCurrentDirectoryFiles();
            if (files.Any())
            {
                var first = files.OrderBy(f => Path.GetFileName(f)).First();
                SetBackgroundImage(Path.GetFullPath(first));
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
