using System;
using System.Text;
using UnityEngine;

namespace Easy.Logging
{
    public class EasyLoggerConsoleAppender : ILogHandler
    {
        private readonly string prefix = "";
        private StringBuilder sb = new StringBuilder();

        public EasyLoggerConsoleAppender(string prefix, string rgba = "000000ff")
        {
        #if (UNITY_EDITOR)
            sb.Append("<color=#");
            sb.Append(rgba);
            sb.Append("><b>");
            sb.Append("[" );sb.Append(prefix);sb.Append("]");
            sb.Append("</b></color>");
            this.prefix = sb.ToString();
        #else
                this.prefix = prefix;
        #endif

        }

        private string FormatMessage(string message)
        {
            sb.Clear();
            sb.Append(prefix);
            sb.Append("[");
            sb.Append(DateTime.Now.ToString("hh:mm:ss.ffff"));
            sb.Append("]");
            sb.AppendLine();
            sb.Append(message);
            return sb.ToString();
        }

        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
        {
            Debug.unityLogger.logHandler.LogFormat(logType, context, FormatMessage(format), args);
        }

        public void LogException(Exception exception, UnityEngine.Object context)
        {
            Debug.unityLogger.LogException(exception, context);
        }
    }
}