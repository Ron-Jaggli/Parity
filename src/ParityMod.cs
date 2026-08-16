using System;
using System.Collections.Generic;
using System.Text;
using MelonLoader;
using Parity.Tweaks;
using UnityEngine;

namespace Parity
{
    /// <summary>
    /// Parity - performance work for BONELAB that leaves the picture alone.
    ///
    /// The rule every tweak in here follows: if a change could alter a pixel, a
    /// sound or a gameplay outcome, it either does not ship or it ships switched
    /// off with an explanation. That rules out the usual easy wins - render scale,
    /// shadow distance, LOD bias, texture quality - and leaves the work that is
    /// simply wasted: shading pixels the lens cannot show, animating skeletons
    /// nobody is looking at, building stack traces for logs nobody reads,
    /// allocating collision objects that are discarded immediately.
    /// </summary>
    public sealed class ParityMod : MelonMod
    {
        /// <summary>
        /// How often engine settings are re-asserted. This is what makes editing
        /// MelonPreferences.cfg take effect without a restart, and it repairs
        /// anything the game resets behind us. Every tweak compares before it
        /// writes, so the steady-state cost is a handful of property reads.
        /// </summary>
        private const float ReapplyIntervalSeconds = 2f;

        private readonly List<Tweak> _tweaks = new List<Tweak>();
        private FrameTimeMonitor _frames;
        private float _nextReapply;
        private bool _enabled;

        public override void OnInitializeMelon()
        {
            ParityLog.Bind(LoggerInstance);
            ParityPreferences.Register();

            _tweaks.Add(new LoggingTweak());
            _tweaks.Add(new VrRenderTweak());
            _tweaks.Add(new RenderingTweak());
            _tweaks.Add(new StreamingTweak());
            _tweaks.Add(new FramePacingTweak());
            _tweaks.Add(new PhysicsTweak());
            _tweaks.Add(new MemoryTweak());
            _tweaks.Add(new AnimatorCullingTweak());
            _tweaks.Add(new AudioListenerTweak());
            _tweaks.Add(new SkinnedMeshTweak());

            _frames = new FrameTimeMonitor();
            _enabled = ParityPreferences.Enabled.Value;

            if (_enabled)
            {
                ApplyAll();
            }

            ParityLog.Info("Parity " + ParityBuildInfo.Version + " loaded. " + DescribeActiveTweaks());
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            // Report what the previous scene measured before discarding it, so
            // moving between areas produces numbers rather than losing them.
            ParityLog.Try("frames.flush", () =>
            {
                float now = Time.realtimeSinceStartup;
                _frames.Flush(now, "scene change");
                _frames.Reset(now, sceneName);
            });

            if (!ParityPreferences.Enabled.Value)
            {
                return;
            }

            // Settings first: the game may have reset some of them during the load.
            ApplyAll();

            for (int i = 0; i < _tweaks.Count; i++)
            {
                Tweak tweak = _tweaks[i];
                try
                {
                    tweak.OnSceneLoaded(sceneName);
                }
                catch (Exception ex)
                {
                    ParityLog.Failed(tweak.Name + ".onSceneLoaded", ex);
                }
            }
        }

        public override void OnUpdate()
        {
            float now = Time.realtimeSinceStartup;

            // Telemetry runs even when the mod is switched off, so that "before" and
            // "after" numbers come from the same measurement.
            _frames.Sample(now, Time.unscaledDeltaTime);

            bool enabled = ParityPreferences.Enabled.Value;
            if (enabled != _enabled)
            {
                _enabled = enabled;

                if (enabled)
                {
                    ApplyAll();
                    ParityLog.Info("Enabled. " + DescribeActiveTweaks());
                }
                else
                {
                    RevertAll();
                    ParityLog.Info("Disabled - vanilla behaviour restored.");
                }
            }

            if (!enabled)
            {
                return;
            }

            if (now >= _nextReapply)
            {
                _nextReapply = now + ReapplyIntervalSeconds;
                ApplyAll();
            }

            for (int i = 0; i < _tweaks.Count; i++)
            {
                Tweak tweak = _tweaks[i];
                try
                {
                    tweak.Tick(now);
                }
                catch (Exception ex)
                {
                    ParityLog.Failed(tweak.Name + ".tick", ex);
                }
            }
        }

        public override void OnDeinitializeMelon()
        {
            // Last chance to say anything: on a headset the session usually ends by
            // the process being killed, and an unreported window is a wasted session.
            ParityLog.Try("frames.final", () => _frames.Flush(Time.realtimeSinceStartup, "shutdown"));
            RevertAll();
        }

        private void ApplyAll()
        {
            for (int i = 0; i < _tweaks.Count; i++)
            {
                Tweak tweak = _tweaks[i];
                try
                {
                    tweak.Apply();
                }
                catch (Exception ex)
                {
                    ParityLog.Failed(tweak.Name + ".apply", ex);
                }
            }
        }

        private void RevertAll()
        {
            for (int i = 0; i < _tweaks.Count; i++)
            {
                Tweak tweak = _tweaks[i];
                try
                {
                    tweak.Revert();
                }
                catch (Exception ex)
                {
                    ParityLog.Failed(tweak.Name + ".revert", ex);
                }
            }
        }

        private static string DescribeActiveTweaks()
        {
            StringBuilder builder = new StringBuilder("Active: ");
            int count = 0;

            Append(builder, ref count, ParityPreferences.StripLogStackTraces.Value, "log stack traces stripped");
            Append(builder, ref count, ParityPreferences.UseOcclusionMesh.Value, "VR occlusion mesh");
            Append(builder, ref count, ParityPreferences.EnsureOcclusionCulling.Value, "occlusion culling");
            Append(builder, ref count, ParityPreferences.DisableRedundantVSync.Value, "no redundant vsync");
            Append(builder, ref count, ParityPreferences.DisableRealtimeReflectionProbes.Value, "no realtime reflection probes");
            Append(builder, ref count, ParityPreferences.DisableOffscreenSkinnedMeshUpdates.Value, "no offscreen skinning");
            Append(builder, ref count, ParityPreferences.TuneAsyncUploads.Value, "async upload buffer");
            Append(builder, ref count, ParityPreferences.ClampMaximumDeltaTime.Value, "delta time clamp");
            Append(builder, ref count, ParityPreferences.ReuseCollisionCallbacks.Value, "collision callback reuse");
            Append(builder, ref count, ParityPreferences.DisableAutoSyncTransforms.Value, "no auto transform sync");
            Append(builder, ref count, ParityPreferences.DisableClothInterCollision.Value, "no cloth inter-collision");
            Append(builder, ref count, ParityPreferences.CullOffscreenAnimators.Value, "animator culling");
            Append(builder, ref count, ParityPreferences.RemoveDuplicateAudioListeners.Value, "audio listener pruning");
            Append(builder, ref count, ParityPreferences.CollectOnSceneLoad.Value, "GC on load");
            Append(builder, ref count, ParityPreferences.UnloadUnusedAssetsOnSceneLoad.Value, "asset unload on load");
            Append(builder, ref count, ParityPreferences.TuneIncrementalGc.Value, "incremental GC slice");

            if (count == 0)
            {
                return "No optimisations are switched on - see UserData/MelonPreferences.cfg.";
            }

            builder.Append('.');
            return builder.ToString();
        }

        private static void Append(StringBuilder builder, ref int count, bool enabled, string label)
        {
            if (!enabled)
            {
                return;
            }

            if (count > 0)
            {
                builder.Append(", ");
            }

            builder.Append(label);
            count++;
        }
    }
}
