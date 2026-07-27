using System;
using System.Collections.Generic;
using MelonLoader;

namespace Parity
{
    /// <summary>
    /// Console output plus the exception guard every tweak runs behind.
    ///
    /// A performance mod that throws inside <c>OnUpdate</c> is worse than no mod at
    /// all: MelonLoader catches it, but it happens again next frame, and the log
    /// spam alone will cost more than anything here saves. So every engine call is
    /// wrapped, and a call site that fails is reported once and then left alone.
    /// </summary>
    internal static class ParityLog
    {
        private static MelonLogger.Instance _logger;
        private static readonly HashSet<string> ReportedFailures = new HashSet<string>();

        public static void Bind(MelonLogger.Instance logger)
        {
            _logger = logger;
        }

        public static void Info(string message)
        {
            if (_logger != null)
            {
                _logger.Msg(message);
            }
            else
            {
                MelonLogger.Msg(BuildInfo.LogPrefix + message);
            }
        }

        public static void Warn(string message)
        {
            if (_logger != null)
            {
                _logger.Warning(message);
            }
            else
            {
                MelonLogger.Warning(BuildInfo.LogPrefix + message);
            }
        }

        /// <summary>
        /// Runs <paramref name="action"/>, swallowing any failure. Returns true when
        /// it completed. <paramref name="site"/> identifies the call for the
        /// report-once behaviour, so pass something stable.
        /// </summary>
        public static bool Try(string site, Action action)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception ex)
            {
                Failed(site, ex);
                return false;
            }
        }

        /// <summary>
        /// Reports a failure from a hand-written try/catch. Use this instead of
        /// <see cref="Try"/> on hot paths: <see cref="Try"/> takes a delegate, and a
        /// delegate that captures anything allocates on every call.
        /// </summary>
        public static void Failed(string site, Exception ex)
        {
            bool isNew;
            lock (ReportedFailures)
            {
                isNew = ReportedFailures.Add(site);
            }

            if (isNew)
            {
                Warn(site + " failed - " + ex.GetType().Name + ": " + ex.Message +
                     ". Further failures at this site will not be logged.");
            }
        }
    }
}
