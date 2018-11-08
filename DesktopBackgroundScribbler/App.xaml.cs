using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace DesktopBackgroundScribbler
{
    /// <summary>
    /// App.xaml の相互作用ロジック
    /// </summary>
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, UseSynchronizationContext = false)]
    public partial class App : Application, IActivatable
    {
        const string id = "{14482529-941C-4025-80F8-1836D76B064B}";
        const string baseAddress = "net.pipe://localhost/DesktopBackgroundScribbler";

        [STAThread]
        public static void Main()
        {
            using (var semaphore = new Semaphore(1, 1, id, out var createdNew))
            {
                if (!createdNew)
                {
                    ActivateExistingWindow();
                    return;
                }

                var app = new App();

                // http://nekojarashi.hatenablog.jp/entry/2017/08/14/WCF_%E3%81%A7%E7%B0%A1%E5%8D%98%E3%81%AB%E3%83%97%E3%83%AD%E3%82%BB%E3%82%B9%E9%96%93%E9%80%9A%E4%BF%A1
                // https://docs.microsoft.com/ja-jp/dotnet/framework/wcf/migrating-from-net-remoting-to-wcf
                // この辺参考にした。
                using (var serviceHost = new ServiceHost(app, new Uri(baseAddress)))
                {
                    serviceHost.AddServiceEndpoint(
                        typeof(IActivatable),
                        new NetNamedPipeBinding(),
                        baseAddress);
                    serviceHost.Open();

                    app.InitializeComponent();
                    app.Run();
                }
            }
        }

        private static void ActivateExistingWindow()
        {
            var binding = new NetNamedPipeBinding();
            var remoteAddress = new EndpointAddress(baseAddress);
            var activatable = ChannelFactory<IActivatable>.CreateChannel(binding, remoteAddress);

            var count = 0;
            while (!activatable.Activate() && count < 10)
            {
                // 最近の Windows は、入力中に突然別のウィンドウにフォーカスが奪われたりすることの無いように、
                // Activate() を呼び出してもアクティブにせず、タスクバーアイコンを点滅させるだけで false を返すことがある。
                // このような場合、以下の Sleep() が無いと、大量に Activate() を呼び出してしまい、タスクバーアイコンの点滅がちらつく。
                // これを防ぐため、Sleep() で1秒おきにしか Activate() を呼び出さないようにしている。
                Thread.Sleep(1000);
                count++;
            }

            (activatable as IClientChannel)?.Close();
        }

        public bool Activate()
        {
            return Dispatcher?.Invoke(() => MainWindow?.Activate()) ?? false;
        }
    }
}
