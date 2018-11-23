using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Interop;

namespace DesktopBackgroundScribbler
{
    /// <summary>
    /// App.xaml の相互作用ロジック
    /// </summary>
    public partial class App : Application
    {
        const string id = "{14482529-941C-4025-80F8-1836D76B064B}";
        const string memoryMappedFileName = id + ".dat";

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [STAThread]
        public static void Main()
        {
            using (var semaphore = new Semaphore(1, 1, id, out var createdNew))
            {
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

                if (!createdNew)
                {
                    ActivateExistingWindow();
                    return;
                }

                var app = new App();
                app.InitializeComponent();

                var mainWindow = new MainWindow();

                using (var mmf = MemoryMappedFile.CreateNew(memoryMappedFileName, 8))
                {
                    mainWindow.Loaded += (sender, e) =>
                    {
                        var windowHandle = new WindowInteropHelper(mainWindow).Handle.ToInt64();

                        using (var stream = mmf.CreateViewStream())
                        {
                            var binaryWriter = new BinaryWriter(stream);
                            binaryWriter.Write(windowHandle);
                        }
                    };

                    app.Run(mainWindow);
                }
            }
        }

        private static void ActivateExistingWindow()
        {
            var count = 0;
            do
            {
                try
                {
                    using (var mmf = MemoryMappedFile.OpenExisting(memoryMappedFileName))
                    using (var stream = mmf.CreateViewStream(0, 8, MemoryMappedFileAccess.Read))
                    {
                        var binaryReader = new BinaryReader(stream);
                        var windowHandle = binaryReader.ReadInt64();
                        if (windowHandle > 0)
                        {
                            SetForegroundWindow(new IntPtr(windowHandle));
                            return;
                        }
                    }
                }
                catch (FileNotFoundException) { }

                Thread.Sleep(1000);
            } while (++count < 10);
        }

        public static void Log(object message)
        {
            var dateTimeStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var log = new StringBuilder(dateTimeStr).AppendLine(" ----------------").Append(message).AppendLine();
            var fileInfo = new FileInfo("Log.log");

            if (!fileInfo.Exists)
            {
                using (var stream = fileInfo.CreateText())
                {
                    stream.Write(log);
                }
                return;
            }

            if (fileInfo.Length < 1024 * 1024)
            {
                using (var stream = fileInfo.AppendText())
                {
                    stream.Write(log);
                }
                return;
            }

            string text;
            using (var stream = fileInfo.OpenText())
            {
                text = stream.ReadToEnd();
            }
            text = text.Substring(text.Length / 2);
            using (var stream = fileInfo.CreateText())
            {
                stream.Write(text);
                stream.Write(log);
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                Log(e?.ExceptionObject);
            }
            catch
            {
                Console.WriteLine("エラーが発生しました。");
                Console.WriteLine(e?.ExceptionObject);
            }
        }
    }
}
