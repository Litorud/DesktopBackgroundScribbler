using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DesktopBackgroundScribbler
{
    static class Logger
    {
        public static void Log(object message)
        {
            var dateTimeStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var log = new StringBuilder(dateTimeStr)
                .AppendLine(" ----------------")
                .Append(message)
                .AppendLine();
            var fileInfo = new FileInfo("Log.log");

            if (!fileInfo.Exists)
            {
                Create(fileInfo, log);
            }
            else if (fileInfo.Length < 1024 * 1024)
            {
                Append(fileInfo, log);
            }
            else
            {
                TruncateAndWrite(fileInfo, log);
            }
        }

        private static void Create(FileInfo fileInfo, object log)
        {
            using (var stream = fileInfo.CreateText())
            {
                stream.Write(log);
            }
        }

        private static void Append(FileInfo fileInfo, object log)
        {
            using (var stream = fileInfo.AppendText())
            {
                stream.Write(log);
            }
        }

        private static void TruncateAndWrite(FileInfo fileInfo, object log)
        {
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
    }
}
