using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Easy.Logging
{
    public class EasyLoggerConfig
    {
        public const string DEFAULT = "Default";
        public interface ILoggerSetting
        {
            string Pattern { get; }
            LogType LogLevel { get; }
            bool Enabled { get; set; }

            bool IsMatching(Type type);
        }

        public class PatternLevelLoggerSetting : ILoggerSetting
        {
            public string Pattern { get; set; }
            private Regex Matcher { get; set; }
            public LogType LogLevel { get; set; }
            public bool Enabled { get; set; }

            public PatternLevelLoggerSetting(string pattern, LogType logLevel, bool enabled)
            {
                Pattern = pattern;
                Matcher = Easy.Utils.RegexUtils.WildcardMatcher(Pattern);
                LogLevel = logLevel;
                Enabled = enabled;
            }

            public bool IsMatching(Type type)
            {
                return Matcher.IsMatch(type.FullName);
            }
        }

        public class ClassLevelLoggerSetting : ILoggerSetting
        {
            public Type ClassType { get; set; }
            public string Pattern { get => ClassType.FullName; }
            public LogType LogLevel { get; set; }
            public bool Enabled { get; set; }

            public ClassLevelLoggerSetting(Type type, LogType logLevel, bool enabled)
            {
                ClassType = type;
                LogLevel = logLevel;
                Enabled = enabled;
            }

            public bool IsMatching(Type type)
            {
                return ClassType.Equals(type);
            }
        }

        private readonly Dictionary<string, ILoggerSetting> settingItems = new Dictionary<string, ILoggerSetting>(){{DEFAULT, new PatternLevelLoggerSetting(DEFAULT, LogType.Log, true) }};

        public event Action OnSettingsChanged;

        public EasyLoggerConfig(Action OnSettingsChangedHandler = null)
        {
            if(OnSettingsChangedHandler != null)
                OnSettingsChanged += OnSettingsChangedHandler;
        }
        public void Apply(LoggerConfigData configData)
        {
            foreach (var logLevel in configData.Logging.LogLevel)
            {
                switch (logLevel.Type)
                {
                    case DEFAULT:
                        {
                            Setup(DEFAULT, stringToType(logLevel.Level), logLevel.Enabled);
                            break;
                        }
                    case "Class":
                        {
                            
                            var type = Type.GetType(logLevel.Pattern);
                            if(type == null){
                                Debug.LogWarningFormat("cannot resolve class type by pattern {0}", logLevel.Pattern);
                                break;
                            }

                            Setup(type, stringToType(logLevel.Level), logLevel.Enabled);
                            break;
                        }
                    case "Pattern":
                        {
                            Setup(logLevel.Pattern, stringToType(logLevel.Level), logLevel.Enabled);
                            break;
                        }
                }
            }

            OnSettingsChanged?.Invoke();
        }

        private LogType stringToType(string level)
        {
            switch (level)
            {
                case "ERROR":
                    return LogType.Error;
                case "WARNING":
                    return LogType.Warning;
                case "LOG":
                    return LogType.Log;
                case "EXCEPTION":
                    return LogType.Exception;
                case "ASSERT":
                    return LogType.Assert;
                default:
                    throw new Exception("Non-existing log level name \"" + level + "\"  ");
            }
        }

        private ILoggerSetting GetBestMatchingSettings(Type type)
        {
            if (settingItems.TryGetValue(type.FullName, out ILoggerSetting settings))
            {
                return settings;
            }

            ILoggerSetting bestMatch = null;
            foreach (var item in settingItems.Values)
            {
                if (item.IsMatching(type) && (bestMatch == null || item.Pattern.Length > bestMatch.Pattern.Length))
                    bestMatch = item;
            }

            if (bestMatch == null && settingItems.TryGetValue(DEFAULT, out settings))
            {
                return settings;
            }

            return bestMatch;
        }
        public LogType GetLogLevel(Type type)
        {
            var settings = GetBestMatchingSettings(type);
            return settings.LogLevel;
        }

        public ILoggerSetting GetLogLevelSettings(Type type)
        {
            return GetBestMatchingSettings(type);
        }


        public void Setup(string pattern, LogType logLevel, bool enabled = true)
        {
            settingItems[pattern] = new PatternLevelLoggerSetting(pattern, logLevel, enabled);
        }

        public void Setup(Type type, LogType logLevel, bool enabled = true)
        {
            settingItems[type.FullName] = new ClassLevelLoggerSetting(type, logLevel, enabled);
        }

    }
}