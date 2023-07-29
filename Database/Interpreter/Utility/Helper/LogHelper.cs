using System;
using System.IO;
using DatabaseInterpreter.Model;

namespace DatabaseInterpreter.Utility
{
    public class LogHelper
    {
        private static readonly object obj = new object();
        public static LogType LogType { get; set; }

        public static void LogInfo(string message)
        {
            Log(LogType.Info, message);
        }

        public static void LogError(string message)
        {
            Log(LogType.Error, message);
        }

        private static void Log(LogType logType, string message)
        {
            var logFolder = "log";

            if (!Directory.Exists(logFolder))
            {
                Directory.CreateDirectory(logFolder);
            }

            var filePath = Path.Combine(logFolder, DateTime.Today.ToString("yyyyMMdd") + ".txt");

            var content = $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}({logType}):{message}";

            lock (obj)
            {
                File.AppendAllLines(filePath, new[] { content });
            }
        }
    }
}