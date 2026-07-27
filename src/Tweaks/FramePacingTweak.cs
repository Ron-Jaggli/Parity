using UnityEngine;

namespace Parity.Tweaks
{
    /// <summary>
    /// Caps how much simulation a single frame is allowed to catch up on.
    ///
    /// Unity ships <c>Time.maximumDeltaTime</c> at 1/3 s. After a 200 ms hitch, the
    /// next frame runs every fixed timestep it missed - in a 60 Hz physics world
    /// that is a dozen full physics steps crammed into one frame. That frame is
    /// therefore also long, which queues more steps, and the stutter sustains
    /// itself. In VR this is the difference between one dropped frame and a
    /// second of judder.
    ///
    /// Clamping the ceiling means the simulation briefly runs slower than real time
    /// instead of trying to make it all up at once. Nothing renders differently; the
    /// world just declines to fast-forward through a stall.
    /// </summary>
    internal sealed class FramePacingTweak : Tweak
    {
        private const int MinSteps = 2;
        private const int MaxSteps = 10;

        public override string Name => "FramePacing";

        private bool _captured;
        private float _originalMaximumDeltaTime;

        public override void Apply()
        {
            ParityLog.Try(Name + ".maximumDeltaTime", () =>
            {
                if (!_captured)
                {
                    _originalMaximumDeltaTime = Time.maximumDeltaTime;
                    _captured = true;
                }

                float want = _originalMaximumDeltaTime;

                if (ParityPreferences.ClampMaximumDeltaTime.Value)
                {
                    int steps = Mathf.Clamp(
                        ParityPreferences.MaxCatchUpPhysicsSteps.Value, MinSteps, MaxSteps);

                    // Recomputed from the live fixed timestep, which the game is free
                    // to change between scenes.
                    float budget = Time.fixedDeltaTime * steps;

                    // Never raise the ceiling, and never drop it below a single step -
                    // that would stall the physics loop entirely.
                    want = Mathf.Clamp(budget, Time.fixedDeltaTime, _originalMaximumDeltaTime);
                }

                if (!Mathf.Approximately(Time.maximumDeltaTime, want))
                {
                    Time.maximumDeltaTime = want;
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
                if (!Mathf.Approximately(Time.maximumDeltaTime, _originalMaximumDeltaTime))
                {
                    Time.maximumDeltaTime = _originalMaximumDeltaTime;
                }
            });
        }
    }
}
