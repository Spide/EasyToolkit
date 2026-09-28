using System;
using System.Collections.Generic;
using UnityEngine;

namespace Easy.Logging
{
    public static class LoggerFactory
    {
        private static readonly EasyLoggerConfig config = new EasyLoggerConfig(ConfigChanged);
        public static EasyLoggerConfig Config
        {
            get
            {
                return config;
            }
        }

        public static Dictionary<Type, EasyLogger> Loggers => loggers;

        private static readonly Dictionary<Type, EasyLogger> loggers = new Dictionary<Type, EasyLogger>();

        public static EasyLogger CreateLogger(Type forType, Color prefixColor, string prefix)
        {
            EasyLogger logger = new EasyLogger(new EasyLoggerConsoleAppender(prefix, ColorUtility.ToHtmlStringRGBA(prefixColor)));

            Loggers.Add(forType, logger);
            updateLoggerConfig(forType, logger);

            return logger;
        }

        public static void ConfigChanged()
        {
            foreach (var item in Loggers)
            {
                updateLoggerConfig(item.Key, item.Value);
            }
        }

        private static void updateLoggerConfig(Type forType, EasyLogger logger)
        {
            var settings = Config.GetLogLevelSettings(forType);
            logger.logEnabled = settings.Enabled;
            logger.filterLogType = settings.LogLevel;
        }

        public static EasyLogger GetLogger(Type forType, Color prefixColor, string prefix)
        {
            if (Loggers.ContainsKey(forType))
            {
                return Loggers[forType];
            }

            return CreateLogger(forType, prefixColor, prefix);
        }

        public static EasyLogger GetLogger(Type forType, Color prefixColor)
        {
            if (Loggers.ContainsKey(forType))
            {
                return Loggers[forType];
            }

            return CreateLogger(forType, prefixColor, forType.Name);
        }
    }
}