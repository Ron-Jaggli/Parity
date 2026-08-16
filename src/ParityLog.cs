using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MelonLoader;
using UnityEngine;

namespace Parity
{
    /// <summary>
    /// Console output, a private log file, and the exception guard every tweak
    /// runs behind.
    ///
    /// A performance mod that throws inside <c>OnUpdate</c> is worse than no mod at
    /// all: MelonLoader catches it, but it happens again next frame, and the log
    /// spam alone will cost more than anything here saves. So every engine call is
    /// wrapped, and a call site that fails is reported once and then left alone.
    ///
    /// Everything written here is also mirrored to <c>Parity-Report.txt</c>. On a
    /// headset there is no console, and MelonLoader's own log interleaves every mod
    /// you have installed - so the one file you actually want is a small one
    /// containing only this mod's output.
    /// </summary>
    internal static class ParityLog
    {
        private const string ReportFileName = "Parity-Report.txt";

        private static MelonLogger.Instance _logger;
        private static readonly HashSet<string> ReportedFailures = new HashSet<string>();
        private static readonly object FileLock = new object();

        private static string _reportPath;
        private static bool _fileUnavailable;

        /// <summary>Absolute path of the report file, or null if it could not be opened.</summary>
        public static string ReportPath => _reportPath;

        public static void Bind(MelonLogger.Instance logger)
        {
            _logger = logger;
            OpenReportFile();
        }

        /// <summary>
        /// Starts a fresh report for this session, keeping the previous one as
        /// <c>.prev</c>. A file that grew across every session would be the thing
        /// nobody wants to read, which defeats the point of having it.
        /// </summary>
        private static void OpenReportFile()
        {
            try
            {
                string directory = Application.persistentDataPath;
                if (string.IsNullOrEmpty(directory))
                {
                    _fileUnavailable = true;
                    return;
                }

                string path = Path.Combine(directory, ReportFileName);

                if (File.Exists(path))
                {
                    string previous = path + ".prev";
                    if (File.Exists(previous))
                    {
                        File.Delete(previous);
                    }

                    File.Move(path, previous);
                }

                File.WriteAllText(path,
                    "Parity " + ParityBuildInfo.Version + " - session started " +
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) +
                    Environment.NewLine);

                _reportPath = path;
                Info("Writing a report to " + path);
            }
            catch (Exception ex)
            {
                _fileUnavailable = true;
                Warn("Could not open a report file (" + ex.GetType().Name + ": " + ex.Message +
                     "). Console and MelonLoader's own log still have everything.");
            }
        }

        private static void AppendToFile(string level, string message)
        {
            if (_fileUnavailable || _reportPath == null)
            {
                return;
            }

            try
            {
                string line = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) +
                              "  " + level + "  " + message + Environment.NewLine;

                // Append rather than holding a handle: this is written a handful of
                // times a minute at most, and a process that is killed mid-session -
                // which on a headset is how it usually ends - loses nothing.
                lock (FileLock)
                {
                    File.AppendAllText(_reportPath, line);
                }
            }
            catch (Exception)
            {
                // Losing the file is not worth a second failure path. Keep the
                // console output working and stop trying.
                _fileUnavailable = true;
            }
        }

        public static void Info(string message)
        {
            if (_logger != null)
            {
                _logger.Msg(message);
            }
            else
            {
                MelonLogger.Msg(ParityBuildInfo.LogPrefix + message);
            }

            AppendToFile("    ", message);
        }

        public static void Warn(string message)
        {
            if (_logger != null)
            {
                _logger.Warning(message);
            }
            else
            {
                MelonLogger.Warning(ParityBuildInfo.LogPrefix + message);
            }

            AppendToFile("WARN", message);
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
