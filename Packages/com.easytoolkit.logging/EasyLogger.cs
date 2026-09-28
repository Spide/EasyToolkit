using System;
using UnityEngine;

namespace Easy.Logging
{
    public class EasyLogger : Logger
    {
        public EasyLogger(ILogHandler logHandler) : base(logHandler)
        {
        }

        public void LogFormat(string format, params object[] args)
        {
            LogFormat(LogType.Log, format, args);
        }

        public void Log(string text, params object[] args)
        {
            LogFormat(LogType.Log, text, args);
        }

        public void LogWarning(string text, params object[] args)
        {
            LogFormat(LogType.Warning, text, args);
        }

        public void LogError(string text, params object[] args)
        {
            LogFormat(LogType.Error, text, args);
        }

        public void Assert(string text, params object[] args)
        {
            LogFormat(LogType.Assert, text, args);
        }

        public void LogException(Exception exception, string text, params object[] args)
        {
            LogFormat(LogType.Exception, text + "\n"+ exception.Message, args);
        }


    }
}