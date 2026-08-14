using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace Parity.Tweaks
{
    /// <summary>
    /// Stops skinned meshes recomputing their bounds every frame.
    ///
    /// <c>SkinnedMeshRenderer.updateWhenOffscreen</c> makes Unity skin the mesh and
    /// recalculate its bounds on the CPU every frame regardless of visibility. It
    /// exists for meshes whose authored bounds are wrong - typically ones deformed
    /// far outside their bind pose - and it is the standard fix an artist reaches
    /// for when something disappears at the wrong moment. That makes it common in
    /// community content, and on a mobile CPU it is expensive out of all proportion
    /// to what it buys.
    ///
    /// This is off by default and it is the one tweak here most likely to be
    /// visible: if a mesh really does need it, switching it off means the mesh can
    /// vanish while still on screen, because Unity culls it against bounds that no
    /// longer describe where it actually is. Turn it on, look at your avatars and
    /// NPCs, and turn it back off if anything blinks out.
    ///
    /// The sweep deliberately does not share code with AnimatorCullingTweak despite
    /// the shape being near-identical. Factoring it out would mean calling
    /// <c>FindObjectsOfType&lt;T&gt;()</c> through a generic type parameter, and
    /// IL2CPP interop resolves those against the concrete type at call time - it is
    /// exactly the kind of construct that compiles and then fails at runtime. Two
    /// concrete sweeps are worth more than one clever one.
    /// </summary>
    internal sealed class SkinnedMeshTweak : Tweak
    {
        private const int MinBudget = 4;
        private const int MaxBudget = 512;
        private const float MinRescan = 2f;
        private const float MaxRescan = 300f;

        public override string Name => "SkinnedMesh";

        private struct Tracked
        {
            public SkinnedMeshRenderer Renderer;
            public bool OriginalUpdateWhenOffscreen;
        }

        private readonly Dictionary<int, Tracked> _tracked = new Dictionary<int, Tracked>();
        private readonly List<int> _deadIds = new List<int>();

        private Il2CppArrayBase<SkinnedMeshRenderer> _pending;
        private int _cursor;
        private float _nextScan;
        private bool _active;
        private int _changedThisSweep;

        public override void OnSceneLoaded(string sceneName)
        {
            _tracked.Clear();
            _deadIds.Clear();
            _pending = null;
            _cursor = 0;
            _nextScan = 0f;
        }

        public override void Tick(float now)
        {
            if (!ParityPreferences.DisableOffscreenSkinnedMeshUpdates.Value)
            {
                if (_active)
                {
                    RestoreAll();
                    _active = false;
                }

                return;
            }

            _active = true;

            if (_pending != null)
            {
                ProcessSlice();
                return;
            }

            if (now >= _nextScan)
            {
                BeginSweep(now);
            }
        }

        private void BeginSweep(float now)
        {
            float interval = Mathf.Clamp(
                ParityPreferences.SkinnedMeshRescanSeconds.Value, MinRescan, MaxRescan);
            _nextScan = now + interval;
            _changedThisSweep = 0;
            _cursor = 0;

            ParityLog.Try(Name + ".find", () =>
            {
                _pending = UnityEngine.Object.FindObjectsOfType<SkinnedMeshRenderer>();
            });
        }

        private void ProcessSlice()
        {
            int budget = Mathf.Clamp(
                ParityPreferences.SkinnedMeshesPerFrame.Value, MinBudget, MaxBudget);

            int length = _pending.Length;
            int end = Math.Min(_cursor + budget, length);

            for (; _cursor < end; _cursor++)
            {
                try
                {
                    Consider(_pending[_cursor]);
                }
                catch (Exception ex)
                {
                    ParityLog.Failed(Name + ".consider", ex);
                }
            }

            if (_cursor < length)
            {
                return;
            }

            _pending = null;
            PruneDestroyed();

            if (_changedThisSweep > 0)
            {
                ParityLog.Info("Stopped offscreen bounds updates on " + _changedThisSweep +
                               " skinned mesh" + (_changedThisSweep == 1 ? "" : "es") +
                               " (" + length + " scanned).");
            }
        }

        private void Consider(SkinnedMeshRenderer renderer)
        {
            if (renderer == null || !renderer.updateWhenOffscreen)
            {
                return;
            }

            int id = renderer.GetInstanceID();

            if (!_tracked.ContainsKey(id))
            {
                _tracked[id] = new Tracked
                {
                    Renderer = renderer,
                    OriginalUpdateWhenOffscreen = true,
                };
            }

            renderer.updateWhenOffscreen = false;
            _changedThisSweep++;
        }

        /// <summary>
        /// Drops entries whose renderer has been destroyed, so the table stays
        /// bounded in a game built around spawning things.
        /// </summary>
        private void PruneDestroyed()
        {
            _deadIds.Clear();

            foreach (KeyValuePair<int, Tracked> entry in _tracked)
            {
                if (entry.Value.Renderer == null)
                {
                    _deadIds.Add(entry.Key);
                }
            }

            for (int i = 0; i < _deadIds.Count; i++)
            {
                _tracked.Remove(_deadIds[i]);
            }

            _deadIds.Clear();
        }

        private void RestoreAll()
        {
            _pending = null;
            _cursor = 0;

            if (_tracked.Count == 0)
            {
                return;
            }

            ParityLog.Try(Name + ".restore", () =>
            {
                foreach (KeyValuePair<int, Tracked> entry in _tracked)
                {
                    SkinnedMeshRenderer renderer = entry.Value.Renderer;
                    if (renderer != null)
                    {
                        renderer.updateWhenOffscreen = entry.Value.OriginalUpdateWhenOffscreen;
                    }
                }
            });

            _tracked.Clear();
        }

        public override void Revert()
        {
            RestoreAll();
            _active = false;
        }
    }
}
