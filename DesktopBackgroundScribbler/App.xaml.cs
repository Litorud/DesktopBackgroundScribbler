using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Text;
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

                    AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

                    app.InitializeComponent();
                    app.Run();
                }
            }
        }

        private static void ActivateExistingWindow()
        {
            var binding = new NetNamedPipeBinding();
            var address = new EndpointAddress(baseAddress);
            using (var channelFactory = new ChannelFactory<IActivatable>(binding, address))
            {
                var activatable = channelFactory.CreateChannel();

                var count = 0;
                do
                {
                    try
                    {
                        if (activatable.Activate())
                        {
                            break;
                        }
                    }
                    catch (EndpointNotFoundException)
                    {
                        // EndpointNotFoundException が発生した IClientChannel は、状態が Faulted になり、
                        // 以降、通信や Close() を実行しても、CommunicationObjectFaultedException になる。
                        // そのため、新しいインスタンスを activatable に代入する必要がある。
                        // なお、元のインスタンスは Abort() でリソースを解放する必要がある。
                        // https://docs.microsoft.com/ja-jp/dotnet/api/system.servicemodel.icommunicationobject.state?view=netframework-4.7.2#System_ServiceModel_ICommunicationObject_State
                        // 解放しなかった場合、ChannelFactory の Dispose() 時にも CommunicationObjectFaultedException が発生する。
                        // Abort() を呼び出したからといって再利用できるようになるわけではない。
                        (activatable as IClientChannel)?.Abort();
                        activatable = channelFactory.CreateChannel();
                    }

                    // 最近の Windows は、入力中に突然別のウィンドウにフォーカスが奪われたりすることの無いように、
                    // Activate() を呼び出してもアクティブにせず、タスクバーアイコンを点滅させるだけで false を返すことがある。
                    // このような場合、以下の Sleep() が無いと、大量に Activate() を呼び出してしまい、タスクバーアイコンの点滅がちらつく。
                    // これを防ぐため、Sleep() で1秒おきにしか Activate() を呼び出さないようにしている。
                    Thread.Sleep(1000);
                } while (++count < 10);

                (activatable as IClientChannel)?.Close();
            }
        }

        public bool Activate()
        {
            return Dispatcher?.Invoke(() => MainWindow?.Activate()) ?? false;
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
