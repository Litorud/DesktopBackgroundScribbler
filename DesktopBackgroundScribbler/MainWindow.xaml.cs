using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace DesktopBackgroundScribbler
{
    public partial class MainWindow : Window
    {
        public IntPtr Handle { get; private set; }

        public event EventHandler HandleInitialized;

        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // http://grabacr.net/archives/1585
            // によると、ウィンドウハンドルが取れるようになるタイミングは SourceInitialized とのこと。
            // https://docs.microsoft.com/ja-jp/dotnet/framework/wpf/app-development/wpf-windows-overview
            // の「ウィンドウの有効期間イベント」を見ると、SourceInitialized が最も早いタイミングと分かる。
            Handle = new WindowInteropHelper(this).Handle;
            OnHandleInitialized(EventArgs.Empty);

            WindowPlacementManager.Restore(Handle);
        }

        protected virtual void OnHandleInitialized(EventArgs e)
        {
            HandleInitialized?.Invoke(this, EventArgs.Empty);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            text.GetBindingExpression(TextBox.TextProperty).UpdateSource();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            mainWindowModel.SaveHistory();
            WindowPlacementManager.Save(Handle);
        }
    }
}
