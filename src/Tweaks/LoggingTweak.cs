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
    ///
    /// This once also offered to switch the game's logger off entirely, which was
    /// slightly faster again. It does not any more: <c>ILogger.logEnabled</c> is
    /// projected read-only by the IL2CPP interop assemblies, so there is no
    /// supported way to set it. That was a marginal, off-by-default saving whose
    /// own description warned it would hide real errors, so it is no loss.
    /// </summary>
    internal sealed class LoggingTweak : Tweak
    {
        public override string Name => "Logging";

        private bool _captured;
        private StackTraceLogType _originalLog;
        private StackTraceLogType _originalWarning;

        public override void Apply()
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

        public override void Revert()
        {
            if (!_captured)
            {
                return;
            }

            ParityLog.Try(Name + ".revert", () =>
            {
                Application.SetStackTraceLogType(LogType.Log, _originalLog);
                Application.SetStackTraceLogType(LogType.Warning, _originalWarning);
            });
        }
    }
}
