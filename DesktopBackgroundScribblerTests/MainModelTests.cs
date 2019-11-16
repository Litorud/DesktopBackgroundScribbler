using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace DesktopBackgroundScribbler.Tests
{
    [TestClass()]
    public class MainModelTests
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        [TestMethod()]
        public void ScribbleTest()
        {
            var mainModel = new MainModel();

            // 小さな文字が描画できることを確認する。
            mainModel.Scribble("."); S();

            // 縦長の文字が描画できることを確認する。
            mainModel.Scribble("l"); S();

            // 異常に長い文字列が描画できることを確認する。
            mainModel.Scribble("亜唖娃阿哀愛挨姶逢葵茜穐悪握渥旭葦芦鯵梓圧斡扱宛姐虻飴絢綾鮎或粟袷安庵按暗案闇鞍杏以伊位依偉囲夷委威尉惟意慰易椅為畏異移維緯胃萎衣謂違遺医井亥域育郁磯一壱溢逸稲茨芋鰯允印咽員因姻引飲淫胤蔭院陰隠韻吋右宇烏羽迂雨卯鵜窺丑碓臼渦嘘唄欝蔚鰻姥厩浦瓜閏噂云運雲荏餌叡営嬰影映曳栄永泳洩瑛盈穎頴英衛詠鋭液疫益駅悦謁越閲榎厭円園堰奄宴延怨掩援沿演炎焔煙燕猿縁艶苑薗遠鉛鴛塩於汚甥凹央奥往応押旺横欧殴王翁襖鴬鴎黄岡"); S(); // JIS 第1水準の先頭200文字。

            Assert.IsTrue(true);
        }

        [TestMethod()]
        public void UndoTest()
        {
            const string defaultPath = @"C:\Windows\Web\Wallpaper\Windows\img0.jpg";
            const string backup = @"Backup";

            var mainModel = new MainModel();
            var i = 1;

            Initialize(backup);

            // 1度も書いてないとき、何も起こらないことを確認する。
            mainModel.Undo(); S();
            Assert.AreEqual(defaultPath, GetCurrentPath());

            // 1度書く。
            mainModel.Scribble(i++.ToString()); S();

            // 1度書いただけでは、戻そうったって戻せないことを確認する。
            // ※書く前の状態とはどういう状態なのかを、このプログラムが完全に把握することは不可能なため。
            mainModel.Undo(); S();
            Assert.AreEqual(GetCurrentDirectoryFilePath(), GetCurrentPath());

            // もう一度書く。
            mainModel.Scribble(i++.ToString()); S();

            // カレントディレクトリの Background_yyyyMMddTHHmmss,fff.bmp からバックアップに戻せることを確認する。
            mainModel.Undo(); S();
            var backupFile1 = Path.GetFullPath(Directory.EnumerateFiles(backup).OrderByDescending(f => f).First());
            Assert.AreEqual(backupFile1, GetCurrentPath());

            // 限界を超えて戻そうとしても何も起こらないことを確認する。
            mainModel.Undo(); S();
            Assert.AreEqual(backupFile1, GetCurrentPath());

            // もう一度書く。
            mainModel.Scribble(i++.ToString()); S();

            // Backup フォルダー内に何も変わりが無いことを確認する。
            Assert.AreEqual(backupFile1, Path.GetFullPath(Directory.EnumerateFiles(backup).OrderByDescending(f => f).First()));

            // もう一度書く。
            mainModel.Scribble(i++.ToString()); S();

            // バックアップから、さらに古いバックアップに戻せることを確認する。
            mainModel.Undo(); S(); // 最新のバックアップになる。
            mainModel.Undo(); S(); // その前のバックアップになる。はず。
            Assert.AreEqual(backupFile1, GetCurrentPath());

            // もう一度書く。
            mainModel.Scribble(i++.ToString()); S();

            // 2回以上戻った状態から新しく書くと、それより新しいバックアップが消えることを確認する。
            Assert.IsTrue(Directory.EnumerateFiles(backup).Count() == 1);
            Assert.AreEqual(backupFile1, Path.GetFullPath(Directory.EnumerateFiles(backup).OrderByDescending(f => f).First()));

            // 背景をなんか別のにする。
            SystemParametersInfo(20, 0, defaultPath, 1 | 2); S();

            // 知らない背景のとき、戻すと、このプログラムが知っている最も新しい画像が使われることを確認する。
            mainModel.Undo(); S();
            Assert.AreEqual(GetCurrentDirectoryFilePath(), GetCurrentPath());

            // まず戻した状態にして、さらに背景をなんか別のにする。
            mainModel.Undo(); S();
            SystemParametersInfo(20, 0, defaultPath, 1 | 2); S();

            // 以前戻した状態であったとしても、今知らない背景のとき、戻すと、このプログラムが知っている最も新しい画像が使われることを確認する。
            mainModel.Undo(); S();
            Assert.AreEqual(GetCurrentDirectoryFilePath(), GetCurrentPath());

            // 十度書く。
            mainModel.Scribble(i++.ToString()); S();
            mainModel.Scribble(i++.ToString()); S();
            mainModel.Scribble(i++.ToString()); S();
            mainModel.Scribble(i++.ToString()); S();
            mainModel.Scribble(i++.ToString()); S();
            mainModel.Scribble(i++.ToString()); S();
            mainModel.Scribble(i++.ToString()); S();
            mainModel.Scribble(i++.ToString()); S();
            mainModel.Scribble(i++.ToString()); S();
            mainModel.Scribble(i++.ToString()); S();

            // Backup 内には11回ファイルが追加された状態だが、古いファイルが消えてファイルが10個しかないことを確認する。
            Assert.IsTrue(Directory.EnumerateFiles(backup).Count() == 10);
            Assert.IsTrue(Directory.EnumerateFiles(backup).All(f => Path.GetFullPath(f) != backupFile1));

            // 背景に無効なパスを設定する。
            SystemParametersInfo(20, 0, ">", 1 | 2); S();

            // 無効なパスが設定されているとき、戻すと、このプログラムが知っている最も新しい画像が使われることを確認する。
            mainModel.Undo(); S();
            Assert.AreEqual(GetCurrentDirectoryFilePath(), GetCurrentPath());

            // もう一度背景に無効なパスを設定し、カレントディレクトリの Background_yyyyMMddTHHmmss,fff.bmp を削除する。
            SystemParametersInfo(20, 0, ">", 1 | 2); S();
            File.Delete(GetCurrentDirectoryFilePath()); S();

            // 無効なパスが設定されていて、しかもカレントディレクトリの Background_yyyyMMddTHHmmss,fff.bmp が無いときでも、戻すと、このプログラムが知っている最も新しい画像が使われることを確認する。
            mainModel.Undo(); S();
            Assert.AreEqual(Path.GetFullPath(Directory.EnumerateFiles(backup).OrderByDescending(f => f).First()), GetCurrentPath());
        }

        static void Initialize(string backupDirectory)
        {
            // Backup を削除する。
            if (Directory.Exists(backupDirectory))
            {
                Directory.Delete(backupDirectory, true);
            }

            // Background_yyyyMMddTHHmmss,fff.bmp も削除する。
            foreach (var f in Directory.EnumerateFiles(".", "Background_*.bmp"))
            {
                File.Delete(f);
            }

            // 背景をなんか別のにする。
            // これで初起動時の状態になる。
            SystemParametersInfo(20, 0, @"C:\Windows\Web\Wallpaper\Windows\img0.jpg", 1 | 2); S();
        }

        static void S()
        {
            Thread.Sleep(100);
        }

        static string GetCurrentPath()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop"))
            {
                return Convert.ToString(key.GetValue("Wallpaper"));
            }
        }

        private string GetCurrentDirectoryFilePath()
        {
            return Path.GetFullPath(Directory.EnumerateFiles(".", "Background_*.bmp").First());
        }

        [TestMethod()]
        public void RedoTest()
        {
            const string defaultPath = @"C:\Windows\Web\Wallpaper\Windows\img0.jpg";
            const string backup = @"Backup";

            var mainModel = new MainModel();
            var i = 1;

            Initialize(backup);

            // 1度も書いていないとき、何も起こらないことを確認する。
            mainModel.Redo(); S();
            Assert.AreEqual(defaultPath, GetCurrentPath());

            mainModel.Undo(); S();
            mainModel.Redo(); S();
            Assert.AreEqual(defaultPath, GetCurrentPath());

            mainModel.Undo(); S();
            mainModel.Undo(); S();
            mainModel.Redo(); S();
            Assert.AreEqual(defaultPath, GetCurrentPath());

            // 1度書く。
            mainModel.Scribble(i++.ToString()); S();

            // 1度書いただけでは戻せないので、何も起こらないことを確認する。
            mainModel.Redo(); S();
            Assert.AreEqual(GetCurrentDirectoryFilePath(), GetCurrentPath());

            mainModel.Undo(); S();
            mainModel.Redo(); S();
            Assert.AreEqual(GetCurrentDirectoryFilePath(), GetCurrentPath());

            mainModel.Undo(); S();
            mainModel.Undo(); S();
            mainModel.Redo(); S();
            Assert.AreEqual(GetCurrentDirectoryFilePath(), GetCurrentPath());

            // もう一度書く。
            mainModel.Scribble(i++.ToString()); S();

            // 1回戻し、1回やり直せることを確認する。
            mainModel.Undo(); S();
            mainModel.Redo(); S();
            Assert.AreEqual(GetCurrentDirectoryFilePath(), GetCurrentPath());

            // もう一度書く。
            mainModel.Scribble(i++.ToString()); S();

            // 2回戻し、1回やり直せることを確認する。
            mainModel.Undo(); S();
            mainModel.Undo(); S();
            mainModel.Redo(); S();
            Assert.AreEqual(Path.GetFullPath(Directory.EnumerateFiles(backup).OrderByDescending(f => f).First()), GetCurrentPath());

            // もう1回戻し、さらにもう一度書く。
            mainModel.Undo(); S();
            mainModel.Scribble(i++.ToString()); S();

            // もう一度書いたので、やり直す先だった Background.bmp と最新のバックアップは破棄され、もうやり直せないことを確認する。
            mainModel.Redo(); S();
            Assert.AreEqual(GetCurrentDirectoryFilePath(), GetCurrentPath());

            // 1回戻し、さらに背景をなんか別のにする。
            mainModel.Undo(); S();
            SystemParametersInfo(20, 0, defaultPath, 1 | 2); S();

            // 背景を別のにしたので、もうやり直せないことを確認する。
            mainModel.Redo(); S();
            Assert.AreEqual(defaultPath, GetCurrentPath());
        }
    }
}