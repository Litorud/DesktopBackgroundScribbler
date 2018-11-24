using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace DesktopBackgroundScribbler
{
    /// <summary>
    /// App.xaml の相互作用ロジック
    /// </summary>
    public partial class App : Application
    {
        const string id = "{14482529-941C-4025-80F8-1836D76B064B}";
        const string memoryMappedFileName = id + ".dat";

        const int SW_RESTORE = 9;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

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
                    TryActivateExistingWindow();
                    return;
                }

                var app = new App();
                app.InitializeComponent();

                var mainWindow = new MainWindow();

                using (var mmf = MemoryMappedFile.CreateNew(memoryMappedFileName, 8))
                {
                    mainWindow.HandleInitialized += (sender, e) =>
                    {
                        var windowHandle = mainWindow.Handle.ToInt64();

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

        private static void TryActivateExistingWindow()
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
                            ActivateExistingWindow(windowHandle);
                            return;
                        }
                    }
                }
                catch (FileNotFoundException) { }

                Thread.Sleep(1000);
            } while (++count < 10);
        }

        private static void ActivateExistingWindow(long windowHandle)
        {
            var hWnd = new IntPtr(windowHandle);

            if (IsIconic(hWnd))
            {
                ShowWindowAsync(hWnd, SW_RESTORE);
            }

            SetForegroundWindow(hWnd);
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                Logger.Log(e?.ExceptionObject);
            }
            catch
            {
                Console.WriteLine("エラーが発生しました。");
                Console.WriteLine(e?.ExceptionObject);
            }
        }
    }
}
