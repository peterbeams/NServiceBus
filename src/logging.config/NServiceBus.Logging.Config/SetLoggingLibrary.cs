using System;
using NServiceBus.Logging;

namespace NServiceBus
{
    /// <summary>
    /// Class containing extension method to allow users to use Log4Net for logging
    /// </summary>
    public static class SetLoggingLibrary
    {
        /// <summary>
        /// Use Log4Net for logging with the Console Appender at the level of All.
        /// </summary>
        public static Configure Log4Net(this Configure config)
        {
            var appender = Logging.Loggers.Log4NetAdapter.AppenderFactory.CreateConsoleAppender("All");

            return config.Log4Net(appender);
        }

        /// <summary>
        /// Use Log4Net for logging with your own appender type, initializing it as necessary.
        /// Will call 'ActivateOptions()' on the appender for you.
        /// If you don't specify a threshold, will default to Level.Debug.
        /// If you don't specify layout, uses this as a default: %d [%t] %-5p %c [%x] &lt;%X{auth}&gt; - %m%n
        /// </summary>
        public static Configure Log4Net<TAppender>(this Configure config, Action<TAppender> initializeAppender) where TAppender : new()
        {
            var appender = new TAppender();
            initializeAppender(appender);

            return config.Log4Net(appender);
        }

        /// <summary>
        /// Use Log4Net for logging passing in a pre-configured appender.
        /// Will call 'ActivateOptions()' on the appender for you.
        /// If you don't specify a threshold, will default to Level.Debug.
        /// If you don't specify layout, uses this as a default: %d [%t] %-5p %c [%x] &lt;%X{auth}&gt; - %m%n
        /// </summary>
        public static Configure Log4Net(this Configure config, object appenderSkeleton)
        {
            Log4Net();

            string threshold = null;

            var cfg = Configure.GetConfigSection<Config.Logging>();
            if (cfg != null)
            {
                threshold = cfg.Threshold;
            }

            Logging.Loggers.Log4NetAdapter.Configurator.Basic(appenderSkeleton, threshold);

            return config;
        }

        /// <summary>
        /// Configure NServiceBus to use Log4Net without setting a specific appender.
        /// </summary>
        public static void Log4Net()
        {
            Logging.Loggers.Log4NetAdapter.Configurator.Basic();
        }



        /// <summary>
        /// Configure NServiceBus to use Log4Net and specify your own configuration.
        /// Use 'log4net.Config.XmlConfigurator.Configure' as the parameter to get the configuration from the app.config.
        /// </summary>
        public static void Log4Net(Action config)
        {
            Log4Net();

            config();
        }

        public static Configure NLog(this Configure config, params object[] targets)
        {
            string threshold = null;

            var cfg = Configure.GetConfigSection<Config.Logging>();
            if (cfg != null)
            {
                threshold = cfg.Threshold;
            }

            Logging.Loggers.NLogAdapter.Configurator.Basic(targets, threshold);

            return config;
        }

        public static void NLog()
        {
            Logging.Loggers.NLogAdapter.Configurator.Basic();
        }

        public static void Custom(ILoggerFactory loggerFactory)
        {
            LogManager.LoggerFactory = loggerFactory;
        }

        public static bool Log4NetExists
        {
            get { return Type.GetType("log4net.LogManager, log4net") != null; }
        }

        public static bool NLogExists
        {
            get { return Type.GetType("NLog.LogManager, NLog") != null; }
        }
    }
}