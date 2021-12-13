using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Easy.Logging.Module.Tests
{
    public class PatternTestClass {}

    public class Tests
    {
        [Test]
        public void configMatchingTest()
        {
            var config = new EasyLoggerConfig();
            // disable whole namespace using wildcard pattern
            config.Setup("Easy.Logging*", LogType.Exception, false);
            // override disabled child namespace to enable exceptions
            config.Setup("Easy.Logging.Module.Tests*", LogType.Exception);
            // but allow all for Tests class
            config.Setup(typeof(Tests), LogType.Log);
            // and allow only ERROR for class EasyLoggerConfig
            config.Setup(typeof(EasyLoggerConfig), LogType.Error);

            var settings = config.GetLogLevel(typeof(Tests));
            Assert.True(settings == LogType.Log, "all Log levels are enabled for class Tests");

            settings = config.GetLogLevel(typeof(EasyLoggerConfig));
            Assert.True(settings == LogType.Error, "Log level of type ERROR is enabled for class EasyLoggerConfig ");

            settings = config.GetLogLevel(typeof(PatternTestClass));
            Assert.True(settings == LogType.Exception, "pattern match form "+typeof(PatternTestClass).FullName+" should be exception");

            settings = config.GetLogLevel(typeof(EasyLogger));
            Assert.True(settings == LogType.Exception, "Logger for class is disabled by pattern");
        }

    }


}
