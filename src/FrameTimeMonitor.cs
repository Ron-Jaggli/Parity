using System;
using UnityEngine;

namespace Parity
{
    /// <summary>
    /// Frame time telemetry, so the effect of this mod can be measured rather than
    /// imagined. Written to the log and to Parity-Report.txt.
    ///
    /// Deliberately not an on-screen counter: this mod's whole premise is that it
    /// changes nothing you can see, and drawing an overlay in the headset would
    /// break that promise and cost frames of its own.
    ///
    /// Samples land in a fixed histogram rather than a list, so a session lasting
    /// hours costs the same 644 bytes as one lasting a minute and allocates nothing
    /// per frame. Percentiles matter more than an average here - in VR it is the
    /// worst 1% of frames that you actually notice.
    /// </summary>
    internal sealed class FrameTimeMonitor
    {
        private const float BucketMilliseconds = 0.25f;
        private const int BucketCount = 161;
        private const float OverflowThresholdMs = (BucketCount - 1) * BucketMilliseconds;

        private const int MinimumSamplesToReport = 300;

        private readonly int[] _buckets = new int[BucketCount];

        private int _samples;
        private float _worstMs;
        private float _windowStart;
        private bool _windowOpen;
        private bool _skipNextSample;

        /// <summary>
        /// Emits a report for the window so far, if there is enough of it to mean
        /// anything, and starts a new one.
        ///
        /// Without this, a report only ever appeared after a full uninterrupted
        /// interval, and a scene change threw the window away - so a short session,
        /// or one spent moving between areas, produced no numbers at all. That is
        /// the opposite of useful when the whole point is to find out whether any of
        /// this helped.
        /// </summary>
        public void Flush(float now, string reason)
        {
            if (_samples < MinimumSamplesToReport)
            {
                return;
            }

            Report(now - _windowStart, reason);

            Array.Clear(_buckets, 0, _buckets.Length);
            _samples = 0;
            _worstMs = 0f;
            _windowStart = now;
        }

        /// <summary>
        /// Drops the current window. Called on scene load so that the loading hitch
        /// itself does not contaminate a report about steady-state play.
        /// </summary>
        public void Reset()
        {
            Array.Clear(_buckets, 0, _buckets.Length);
            _samples = 0;
            _worstMs = 0f;
            _windowOpen = false;
            _skipNextSample = true;
        }

        public void Sample(float now, float unscaledDeltaSeconds)
        {
            float interval = ParityPreferences.FrameReportSeconds.Value;
            if (interval <= 0f)
            {
                return;
            }

            if (!_windowOpen)
            {
                _windowOpen = true;
                _windowStart = now;
            }

            // The first frame after a reset spans the load itself.
            if (_skipNextSample)
            {
                _skipNextSample = false;
                return;
            }

            float milliseconds = unscaledDeltaSeconds * 1000f;
            if (milliseconds > 0f)
            {
                int bucket = (int)(milliseconds / BucketMilliseconds);
                if (bucket >= BucketCount)
                {
                    bucket = BucketCount - 1;
                }

                _buckets[bucket]++;
                _samples++;

                if (milliseconds > _worstMs)
                {
                    _worstMs = milliseconds;
                }
            }

            if (now - _windowStart >= interval && _samples > 0)
            {
                Report(now - _windowStart, "interval");

                Array.Clear(_buckets, 0, _buckets.Length);
                _samples = 0;
                _worstMs = 0f;
                _windowStart = now;
            }
        }

        private void Report(float windowSeconds, string reason)
        {
            string summary =
                "Frame time over " + windowSeconds.ToString("F0") + " s (" + reason + "): " +
                _samples + " frames, median " + Percentile(0.50f).ToString("F1") +
                " ms, p95 " + Percentile(0.95f).ToString("F1") +
                " ms, p99 " + Percentile(0.99f).ToString("F1") +
                " ms, worst " + _worstMs.ToString("F1") + " ms.";

            int overflow = _buckets[BucketCount - 1];
            if (overflow > 0)
            {
                summary += " " + overflow + " frame" + (overflow == 1 ? "" : "s") +
                           " exceeded " + OverflowThresholdMs.ToString("F0") + " ms.";
            }

            ParityLog.Info(summary);
        }

        /// <summary>
        /// Upper edge of the bucket containing the requested percentile. Resolution
        /// is <see cref="BucketMilliseconds"/>, which is far finer than the
        /// differences worth reacting to.
        /// </summary>
        private float Percentile(float fraction)
        {
            int target = Mathf.Max(1, Mathf.CeilToInt(_samples * fraction));
            int running = 0;

            for (int i = 0; i < BucketCount; i++)
            {
                running += _buckets[i];
                if (running >= target)
                {
                    return (i + 1) * BucketMilliseconds;
                }
            }

            return OverflowThresholdMs;
        }
    }
}
