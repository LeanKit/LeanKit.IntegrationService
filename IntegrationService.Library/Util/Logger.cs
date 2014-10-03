//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using log4net;
using log4net.Config;

namespace IntegrationService.Util
{

    public sealed class Logger
    {
        #region Fields

        static private readonly Logger LoggerLog;
        private readonly ILog _log;

        #endregion
        #region Constructors

        static Logger()
        {
            ConfigureLoggingSystem();
            LoggerLog = GetLogger(typeof(Logger));
            //spit out the assemblies in the current domain.
            LogCurrentAppDomain();
        }

        internal Logger(string name)
        {
            _log = LogManager.GetLogger(name);
        }

        internal Logger(System.Type type)
        {
            _log = LogManager.GetLogger(type);
        }

        #endregion
        #region Properties

        public static bool Configured
        {
            get
            {
                var repository = LogManager.GetRepository();
                return null != repository && repository.Configured;
            }
        }

        /// <summary>
        /// Is debug enabled?
        /// </summary>
        public bool IsDebugEnabled
        {
            get { return _log.IsDebugEnabled; }
        }

        /// <summary>
        /// Is info enabled? 
        /// </summary>
        public bool IsInfoEnabled
        {
            get { return _log.IsInfoEnabled; }
        }

        /// <summary>
        /// Is warn enabled? 
        /// </summary>
        public bool IsWarnEnabled
        {
            get { return _log.IsWarnEnabled; }
        }

        /// <summary>
        /// Is error enabled? 
        /// </summary>
        public bool IsErrorEnabled
        {
            get { return _log.IsErrorEnabled; }
        }

        /// <summary>
        /// Is fatal enabled? 
        /// </summary>
        public bool IsFatalEnabled
        {
            get { return _log.IsFatalEnabled; }
        }

        internal ILog Log
        {
            get { return _log; }
        }

        #endregion
        #region Methods

        #region Application domain assembly logging

        private static void LogCurrentAppDomain()
        {
            LoggerLog.Info("Current application domain's friendly name is [{0}].", AppDomain.CurrentDomain.FriendlyName);
            Assembly[] asms = AppDomain.CurrentDomain.GetAssemblies();
            AppDomain.CurrentDomain.AssemblyLoad += HandleAppDomainAssemblyLoad;
            foreach (Assembly asm in asms)
            {
                if (!asm.IsDynamic) LogAssembly(asm);
            }
        }

        private static void HandleAppDomainAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            var assembly = args.LoadedAssembly;
            if (!assembly.IsDynamic) LogAssembly(assembly);
        }

        private static void LogAssembly(Assembly asm)
        {
            string asmOrigination;
            if (asm.GlobalAssemblyCache)
            {
                asmOrigination = "loaded from the Global Assembly Cache";
            }
            else if (asm is AssemblyBuilder)
            {
                asmOrigination = "built dynamically in memory";
            }
            else
            {
                asmOrigination = "loaded from unknown location";
            }

            LoggerLog.Debug("Assembly [{0}] {1}.", asm.FullName, asmOrigination);
        }

        #endregion
        #region GetLogger

        public static Logger GetLogger(string name)
        {
            return new Logger(name);
        }

        public static Logger GetLogger(System.Type type)
        {
            return new Logger(type);
        }

        #endregion
        #region Configuration

        public static void ConfigureLoggingSystem()
        {
            //this is needed to warm up Visual Studio .Net
            Trace.WriteLine("");
            var log = new Logger(typeof(Logger));
            try
            {
                const string logfile = "logging.config";
                string fullPathToConfigFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, logfile);
                if (File.Exists(fullPathToConfigFile))
                {
                    ConfigureLoggingSystemAndWatchFile(new FileInfo(fullPathToConfigFile));
                    log.Info("Logger initialized with log configuration file {0}[{1}] and set to watch it.", Environment.NewLine, fullPathToConfigFile);
                }
                else
                {
                    BasicConfigurator.Configure();
                    Trace.WriteLine("Unable to find logging configuration file. Logger not configured.");
                    System.Diagnostics.Debug.WriteLine("Unable to find logging configuration file. Logger not configured.");
                }
            }
            catch (Exception e)
            {
                BasicConfigurator.Configure();
                log.Warn(e, "Could not load logging configuration.");
            }
        }

        public static void ResetConfiguration()
        {
            LogManager.ResetConfiguration();
        }

        public static void ConfigureLoggingSystem(Stream domConfigStream)
        {
            XmlConfigurator.Configure(domConfigStream);
        }

        public static void ConfigureLoggingSystem(FileInfo domConfigFile)
        {
            XmlConfigurator.Configure(domConfigFile);
        }

        public static void ConfigureLoggingSystemAndWatchFile(FileInfo domConfigFile)
        {
            XmlConfigurator.ConfigureAndWatch(domConfigFile);
        }

        #endregion

        #region Log methods

        #region (object value) overloads


        public void Debug(object value)
        {
            WriteIfEnabled(LogLevel.Debug, value);
        }

        public void Info(object value)
        {
            WriteIfEnabled(LogLevel.Info, value);
        }


        public void Warn(object value)
        {
            WriteIfEnabled(LogLevel.Warn, value);
        }


        public void Error(object value)
        {
            WriteIfEnabled(LogLevel.Error, value);
        }


        public void Fatal(object value)
        {
            WriteIfEnabled(LogLevel.Fatal, value);
        }

        #endregion
        #region (Exception ex, object value) overloads


        public void Debug(Exception ex, object value)
        {
            WriteIfEnabled(LogLevel.Debug, ex, value);
        }


        public void Info(Exception ex, object value)
        {
            WriteIfEnabled(LogLevel.Info, ex, value);
        }

        public void Warn(Exception ex, object value)
        {
            WriteIfEnabled(LogLevel.Warn, ex, value);
        }

        public void Error(Exception ex, object value)
        {
            WriteIfEnabled(LogLevel.Error, ex, value);
        }

        public void Fatal(Exception ex, object value)
        {
            WriteIfEnabled(LogLevel.Fatal, ex, value);
        }

        #endregion
        #region (string message) overloads

        public void Debug(string message)
        {
            WriteIfEnabled(LogLevel.Debug, message);
        }

        public void Info(string message)
        {
            WriteIfEnabled(LogLevel.Info, message);
        }

        public void Warn(string message)
        {
            WriteIfEnabled(LogLevel.Warn, message);
        }

        public void Error(string message)
        {
            WriteIfEnabled(LogLevel.Error, message);
        }

        public void Fatal(string message)
        {
            WriteIfEnabled(LogLevel.Fatal, message);
        }

        #endregion
        #region (Exception ex, string message) overloads

        public void Debug(Exception ex, string message)
        {
            WriteIfEnabled(LogLevel.Debug, ex, message);
        }

        public void Info(Exception ex, string message)
        {
            WriteIfEnabled(LogLevel.Info, ex, message);
        }

        public void Warn(Exception ex, string message)
        {
            WriteIfEnabled(LogLevel.Warn, ex, message);
        }

        public void Error(Exception ex, string message)
        {
            WriteIfEnabled(LogLevel.Error, ex, message);
        }

        public void Fatal(Exception ex, string message)
        {
            WriteIfEnabled(LogLevel.Fatal, ex, message);
        }

        #endregion
        #region (string message, params object[] formatArgs) overloads

        public void Debug(string message, params object[] formatArgs)
        {
            WriteIfEnabled(LogLevel.Debug, message, formatArgs);
        }

        public void Info(string message, params object[] formatArgs)
        {
            WriteIfEnabled(LogLevel.Info, message, formatArgs);
        }

        public void Warn(string message, params object[] formatArgs)
        {
            WriteIfEnabled(LogLevel.Warn, message, formatArgs);
        }

        public void Error(string message, params object[] formatArgs)
        {
            WriteIfEnabled(LogLevel.Error, message, formatArgs);
        }

        public void Fatal(string message, params object[] formatArgs)
        {
            WriteIfEnabled(LogLevel.Fatal, message, formatArgs);
        }

        #endregion
        #region (Exception ex, string message, params object[] formatArgs) overloads

        public void Debug(Exception ex, string message, params object[] formatArgs)
        {
            WriteIfEnabled(LogLevel.Debug, ex, message, formatArgs);
        }

        public void Info(Exception ex, string message, params object[] formatArgs)
        {
            WriteIfEnabled(LogLevel.Info, ex, message, formatArgs);
        }

        public void Warn(Exception ex, string message, params object[] formatArgs)
        {
            WriteIfEnabled(LogLevel.Warn, ex, message, formatArgs);
        }

        public void Error(Exception ex, string message, params object[] formatArgs)
        {
            WriteIfEnabled(LogLevel.Error, ex, message, formatArgs);
        }

        public void Fatal(Exception ex, string message, params object[] formatArgs)
        {
            WriteIfEnabled(LogLevel.Fatal, ex, message, formatArgs);
        }

        #endregion

        #endregion
        #region Private LogLevel-generalized methods

        #region WriteIfEnabled ()


        private void WriteIfEnabled(LogLevel level, object value)
        {
            if (IsEnabled(level))
            {
                Write(level, value);
            }
        }

        private void WriteIfEnabled(LogLevel level, Exception exc, object value)
        {
            if (IsEnabled(level))
            {
                Write(level, exc, value);
            }
        }

        private void WriteIfEnabled(LogLevel level, string message)
        {
            if (IsEnabled(level))
            {
                Write(level, message);
            }
        }

        private void WriteIfEnabled(LogLevel level, Exception exc, string message)
        {
            if (IsEnabled(level))
            {
                Write(level, exc, message);
            }
        }

        private void WriteIfEnabled(LogLevel level, string message, params object[] formatArgs)
        {
            if (IsEnabled(level))
            {
                Write(level, message.FormatSafely(formatArgs));
            }
        }

        private void WriteIfEnabled(LogLevel level, Exception exc, string message, params object[] formatArgs)
        {
            if (IsEnabled(level))
            {

                Write(level, exc, message.FormatSafely(formatArgs));
            }
        }

        #endregion
        #region Write ()

        private void Write(LogLevel level, object value)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    _log.Debug(value);
                    break;
                case LogLevel.Info:
                    _log.Info(value);
                    break;
                case LogLevel.Warn:
                    _log.Warn(value);
                    break;
                case LogLevel.Error:
                    _log.Error(value);
                    break;
                default:  // LogLevel.Fatal :
                    _log.Fatal(value);
                    break;
            }
        }


        private void Write(LogLevel level, Exception exc, object value)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    _log.Debug(value, exc);
                    break;
                case LogLevel.Info:
                    _log.Info(value, exc);
                    break;
                case LogLevel.Warn:
                    _log.Warn(value, exc);
                    break;
                case LogLevel.Error:
                    _log.Error(value, exc);
                    break;
                default:  // LogLevel.Fatal :
                    _log.Fatal(value, exc);
                    break;
            }
        }

        #endregion


        /// <summary>Is a specified <see cref="LogLevel" /> enabled?</summary>
        /// <param name="level">The level to check.</param>
        private bool IsEnabled(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    return _log.IsDebugEnabled;
                case LogLevel.Info:
                    return _log.IsInfoEnabled;
                case LogLevel.Warn:
                    return _log.IsWarnEnabled;
                case LogLevel.Error:
                    return _log.IsErrorEnabled;
                default:  // LogLevel.Fatal :
                    return _log.IsFatalEnabled;
            }
        }

        #endregion

        #endregion
        #region Nested Type

        public enum LogLevel
        {
            /// <summary>The <see cref="Logger.LogLevel.Debug" /> level is the lowest level used for diagnostics.</summary>
            Debug = 30000,

            /// <summary>The <see cref="Logger.LogLevel.Info" /> level is intended for information detail.</summary>
            Info = 40000,

            /// <summary>The <see cref="Logger.LogLevel.Warn" /> level is intended to indicate potential problems.</summary>
            Warn = 60000,

            /// <summary>The <see cref="Logger.LogLevel.Error" /> level is intended to indicate a condition that may cause system failure or bad data if not handled.</summary>
            Error = 70000,

            /// <summary>The <see cref="Logger.LogLevel.Fatal" /> level is intended to indicate a condition cannot be recovered from by the application.</summary>
            Fatal = 110000
        };

        #endregion
    }
}