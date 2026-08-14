using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.XR;

namespace Parity.Tweaks
{
    /// <summary>
    /// Three rendering-side settings that cost frames without drawing anything.
    ///
    /// Occlusion culling and the VR vsync clamp are on by default and cannot
    /// change what you see. Realtime reflection probes can, so that one ships off.
    /// </summary>
    internal sealed class RenderingTweak : Tweak
    {
        public override string Name => "Rendering";

        // Cameras whose occlusion culling we switched on, so it can be undone.
        private readonly List<Camera> _enabledOcclusion = new List<Camera>();

        private bool _capturedVSync;
        private int _originalVSync;

        private bool _capturedProbes;
        private bool _originalRealtimeProbes;

        public override void Apply()
        {
            ApplyVSync();
            ApplyReflectionProbes();
        }

        /// <summary>
        /// In VR the headset's compositor decides when a frame is presented. Unity's
        /// own vsync wait is a second, redundant gate on top of that - it blocks on a
        /// desktop vblank that has nothing to do with the display you are looking
        /// through. Zero is what XR plugins expect; anything else is wasted latency.
        ///
        /// Only touched while XR is actually running, and never outside VR, where
        /// vsync is doing a real job.
        /// </summary>
        private void ApplyVSync()
        {
            ParityLog.Try(Name + ".vSync", () =>
            {
                if (!XRSettings.enabled)
                {
                    return;
                }

                if (!_capturedVSync)
                {
                    _originalVSync = QualitySettings.vSyncCount;
                    _capturedVSync = true;
                }

                int want = ParityPreferences.DisableRedundantVSync.Value ? 0 : _originalVSync;
                if (QualitySettings.vSyncCount != want)
                {
                    QualitySettings.vSyncCount = want;
                }
            });
        }

        /// <summary>
        /// A reflection probe set to refresh every frame re-renders the scene six
        /// times to fill a cubemap. On a mobile GPU that is ruinous, and community
        /// content sets it by accident more often than on purpose.
        ///
        /// Off by default all the same: if a probe genuinely is meant to update,
        /// disabling it freezes the reflection, and a frozen reflection is something
        /// you can see.
        /// </summary>
        private void ApplyReflectionProbes()
        {
            if (!ParityPreferences.DisableRealtimeReflectionProbes.Value && !_capturedProbes)
            {
                return;
            }

            ParityLog.Try(Name + ".reflectionProbes", () =>
            {
                if (!_capturedProbes)
                {
                    _originalRealtimeProbes = QualitySettings.realtimeReflectionProbes;
                    _capturedProbes = true;
                }

                bool want = ParityPreferences.DisableRealtimeReflectionProbes.Value
                    ? false
                    : _originalRealtimeProbes;

                if (QualitySettings.realtimeReflectionProbes != want)
                {
                    QualitySettings.realtimeReflectionProbes = want;
                }
            });
        }

        /// <summary>
        /// Occlusion culling only ever skips renderers the baked data proves cannot
        /// be seen from where you are standing. Turning it on can therefore remove
        /// work but never remove anything visible - and if the scene ships no
        /// occlusion data, it simply does nothing.
        ///
        /// Cameras are few, so this runs once per scene rather than on a budget.
        /// A camera that already had it on is left alone and not tracked, so
        /// reverting can never turn off something the game wanted.
        /// </summary>
        public override void OnSceneLoaded(string sceneName)
        {
            _enabledOcclusion.Clear();

            if (!ParityPreferences.EnsureOcclusionCulling.Value)
            {
                return;
            }

            ParityLog.Try(Name + ".occlusionCulling", () =>
            {
                Il2CppArrayBase<Camera> cameras = UnityEngine.Object.FindObjectsOfType<Camera>();

                for (int i = 0; i < cameras.Length; i++)
                {
                    Camera camera = cameras[i];
                    if (camera == null || camera.useOcclusionCulling)
                    {
                        continue;
                    }

                    camera.useOcclusionCulling = true;
                    _enabledOcclusion.Add(camera);
                }

                if (_enabledOcclusion.Count > 0)
                {
                    ParityLog.Info("Enabled occlusion culling on " + _enabledOcclusion.Count +
                                   " camera" + (_enabledOcclusion.Count == 1 ? "" : "s") + ".");
                }
            });
        }

        public override void Revert()
        {
            ParityLog.Try(Name + ".revert", () =>
            {
                if (_capturedVSync && QualitySettings.vSyncCount != _originalVSync)
                {
                    QualitySettings.vSyncCount = _originalVSync;
                }

                if (_capturedProbes &&
                    QualitySettings.realtimeReflectionProbes != _originalRealtimeProbes)
                {
                    QualitySettings.realtimeReflectionProbes = _originalRealtimeProbes;
                }

                for (int i = 0; i < _enabledOcclusion.Count; i++)
                {
                    Camera camera = _enabledOcclusion[i];
                    if (camera != null)
                    {
                        camera.useOcclusionCulling = false;
                    }
                }
            });

            _enabledOcclusion.Clear();
        }
    }
}
