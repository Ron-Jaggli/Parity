using UnityEngine;

namespace Parity.Tweaks
{
    /// <summary>
    /// Stops Unity walking the managed stack for routine log messages.
    ///
    /// Capturing a stack trace is not free - it is a managed stack walk plus string
    /// building, per call, on the calling thread. A game that logs even a few times
    /// per frame pays for it every frame, and none of that work draws anything.
    /// Errors, asserts and exceptions are deliberately left alone so that when
    /// something does go wrong the log is still worth reading.
    /// </summary>
    internal sealed class LoggingTweak : Tweak
    {
        public override string Name => "Logging";

        private bool _captured;
        private StackTraceLogType _originalLog;
        private StackTraceLogType _originalWarning;
        private bool _loggerWasEnabled = true;
        private bool _loggerCaptured;

        public override void Apply()
        {
            ApplyStackTraces();
            ApplyLoggerSwitch();
        }

        private void ApplyStackTraces()
        {
            ParityLog.Try(Name + ".stackTraces", () =>
            {
                if (!_captured)
                {
                    _originalLog = Application.GetStackTraceLogType(LogType.Log);
                    _originalWarning = Application.GetStackTraceLogType(LogType.Warning);
                    _captured = true;
                }

                bool strip = ParityPreferences.StripLogStackTraces.Value;
                StackTraceLogType wantLog = strip ? StackTraceLogType.None : _originalLog;
                StackTraceLogType wantWarning = strip ? StackTraceLogType.None : _originalWarning;

                if (Application.GetStackTraceLogType(LogType.Log) != wantLog)
                {
                    Application.SetStackTraceLogType(LogType.Log, wantLog);
                }

                if (Application.GetStackTraceLogType(LogType.Warning) != wantWarning)
                {
                    Application.SetStackTraceLogType(LogType.Warning, wantWarning);
                }
            });
        }

        private void ApplyLoggerSwitch()
        {
            ParityLog.Try(Name + ".unityLogger", () =>
            {
                if (!_loggerCaptured)
                {
                    _loggerWasEnabled = Debug.unityLogger.logEnabled;
                    _loggerCaptured = true;
                }

                bool want = ParityPreferences.DisableUnityLogger.Value ? false : _loggerWasEnabled;
                if (Debug.unityLogger.logEnabled != want)
                {
                    Debug.unityLogger.logEnabled = want;
                }
            });
        }

        public override void Revert()
        {
            ParityLog.Try(Name + ".revert", () =>
            {
                if (_captured)
                {
                    Application.SetStackTraceLogType(LogType.Log, _originalLog);
                    Application.SetStackTraceLogType(LogType.Warning, _originalWarning);
                }

                if (_loggerCaptured)
                {
                    Debug.unityLogger.logEnabled = _loggerWasEnabled;
                }
            });
        }
    }
}
