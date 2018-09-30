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
        const string address = "App";

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
                app.InitializeComponent();

                // http://nekojarashi.hatenablog.jp/entry/2017/08/14/WCF_%E3%81%A7%E7%B0%A1%E5%8D%98%E3%81%AB%E3%83%97%E3%83%AD%E3%82%BB%E3%82%B9%E9%96%93%E9%80%9A%E4%BF%A1
                // この辺参考にした。
                using (var serviceHost = new ServiceHost(app, new Uri(baseAddress)))
                {
                    serviceHost.AddServiceEndpoint(
                        typeof(IActivatable),
                        new NetNamedPipeBinding(),
                        address);
                    serviceHost.Open();

                    app.Run();
                }
            }
        }

        private static void ActivateExistingWindow()
        {
            var binding = new NetNamedPipeBinding();
            var remoteAddress = new EndpointAddress($"{baseAddress}/{address}");
            using (var channelFactory = new ChannelFactory<IActivatable>(binding, remoteAddress))
            {
                var channel = channelFactory.CreateChannel();

                bool activated;
                var limit = DateTime.Now.AddSeconds(10);
                do
                {
                    activated = channel.Activate();
                } while (!activated && DateTime.Now < limit);

                (channel as IClientChannel)?.Close();
            }
        }

        public bool Activate()
        {
            var dispatcherOperation = Current?.Dispatcher?.BeginInvoke(new Func<bool>(() =>
            {
                return Current.MainWindow?.Activate() ?? false;
            }));

            return dispatcherOperation?.Result is bool b ? b : false;
        }
    }
}
