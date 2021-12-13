using System;
using System.Collections.Generic;

namespace Easy.Logging
{
    [Serializable]
    public class LoggerConfigData
    {
        public LoggingConfig Logging = new LoggingConfig();

        [Serializable]
        public class LoggingConfig
        {
            public List<LogLevelItem> LogLevel = new List<LogLevelItem>();

            [Serializable]
            public class LogLevelItem
            {
                public string Pattern;
                public string Level;
                public string Type;
                public bool Enabled;
            }
        }

    }
}