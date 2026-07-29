using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace Parity.Tweaks
{
    /// <summary>
    /// Stops animators doing work for skeletons nobody is looking at.
    ///
    /// An animator left on <c>AlwaysAnimate</c> evaluates its state machine, blends
    /// clips, solves IK and writes a full transform hierarchy every frame, whether
    /// or not its renderers are on screen. Switching to
    /// <c>CullUpdateTransforms</c> keeps the state machine and animation events
    /// running - so scripted behaviour is untouched - and skips only the transform
    /// writes, IK and retargeting, none of which can be observed while the thing is
    /// off screen. When it comes back into view Unity resumes on the frame it
    /// becomes visible again.
    ///
    /// Two guards keep this honest:
    /// <list type="bullet">
    /// <item>Animators using root motion are skipped outright. Culling those would
    /// stop them physically moving while off screen, which is a gameplay change,
    /// not a rendering one.</item>
    /// <item>The player rig is skipped by name, because its pose is read by grab
    /// and locomotion code even on frames where no camera can see it.</item>
    /// </list>
    ///
    /// The sweep itself is budgeted across frames. An optimiser that stalls a frame
    /// while it enumerates every component in the scene has defeated its own purpose.
    /// </summary>
    internal sealed class AnimatorCullingTweak : Tweak
    {
        private const int MinBudget = 4;
        private const int MaxBudget = 512;
        private const float MinRescan = 2f;
        private const float MaxRescan = 300f;

        public override string Name => "AnimatorCulling";

        /// <summary>
        /// An animator we have changed, and the mode it had before we touched it.
        /// The reference is kept so that undoing our work never needs another scene
        /// -wide search, and so that entries for destroyed objects can be spotted
        /// and dropped.
        /// </summary>
        private struct Tracked
        {
            public Animator Animator;
            public AnimatorCullingMode OriginalMode;
        }

        private readonly Dictionary<int, Tracked> _tracked = new Dictionary<int, Tracked>();
        private readonly List<int> _deadIds = new List<int>();

        private Il2CppArrayBase<Animator> _pending;
        private int _cursor;
        private float _nextScan;
        private bool _active;
        private int _changedThisSweep;

        private string _exclusionSource;
        private string[] _exclusions = Array.Empty<string>();

        public override void OnSceneLoaded(string sceneName)
        {
            // Everything we were tracking belonged to the scene being replaced.
            _tracked.Clear();
            _deadIds.Clear();
            _pending = null;
            _cursor = 0;
            _nextScan = 0f;
        }

        public override void Tick(float now)
        {
            if (!ParityPreferences.CullOffscreenAnimators.Value)
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
                ParityPreferences.AnimatorRescanSeconds.Value, MinRescan, MaxRescan);
            _nextScan = now + interval;
            _changedThisSweep = 0;
            _cursor = 0;

            ParityLog.Try(Name + ".find", () =>
            {
                // Active animators only. An inactive one is not costing anything, and
                // a later sweep will catch it if it wakes up.
                _pending = UnityEngine.Object.FindObjectsOfType<Animator>();
            });
        }

        private void ProcessSlice()
        {
            int budget = Mathf.Clamp(
                ParityPreferences.AnimatorsPerFrame.Value, MinBudget, MaxBudget);

            AnimatorCullingMode desired = ParityPreferences.AggressiveAnimatorCulling.Value
                ? AnimatorCullingMode.CullCompletely
                : AnimatorCullingMode.CullUpdateTransforms;

            RefreshExclusions();

            int length = _pending.Length;
            int end = Math.Min(_cursor + budget, length);

            for (; _cursor < end; _cursor++)
            {
                // Deliberately not ParityLog.Try: this runs once per animator per
                // sweep, and a capturing lambda would allocate on every iteration.
                try
                {
                    Consider(_pending[_cursor], desired);
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
                ParityLog.Info("Culling " + _changedThisSweep + " offscreen animator" +
                               (_changedThisSweep == 1 ? "" : "s") + " (" + length + " scanned).");
            }
        }

        private void Consider(Animator animator, AnimatorCullingMode desired)
        {
            if (animator == null)
            {
                return;
            }

            int id = animator.GetInstanceID();

            if (!_tracked.ContainsKey(id))
            {
                // Root motion drives the transform from the animation itself. Cull it
                // and the object stops moving while off screen - a gameplay change.
                if (animator.applyRootMotion)
                {
                    return;
                }

                // Nothing to evaluate, so nothing to save.
                if (animator.runtimeAnimatorController == null)
                {
                    return;
                }

                if (IsExcluded(animator))
                {
                    return;
                }

                _tracked[id] = new Tracked
                {
                    Animator = animator,
                    OriginalMode = animator.cullingMode,
                };
            }

            if (animator.cullingMode != desired)
            {
                animator.cullingMode = desired;
                _changedThisSweep++;
            }
        }

        private bool IsExcluded(Animator animator)
        {
            if (_exclusions.Length == 0)
            {
                return false;
            }

            string own = animator.gameObject.name;
            string root = animator.transform.root.name;

            for (int i = 0; i < _exclusions.Length; i++)
            {
                string needle = _exclusions[i];
                if (own.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    root.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshExclusions()
        {
            string configured = ParityPreferences.AnimatorExcludeNames.Value ?? string.Empty;
            if (string.Equals(configured, _exclusionSource, StringComparison.Ordinal))
            {
                return;
            }

            _exclusionSource = configured;
            _exclusions = configured.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < _exclusions.Length; i++)
            {
                _exclusions[i] = _exclusions[i].Trim();
            }
        }

        /// <summary>
        /// Drops entries for animators that have since been destroyed. Without this
        /// the table would grow for the whole time a scene is loaded, which in a game
        /// built around spawning things is not a bounded amount. Dictionary iteration
        /// here uses the struct enumerator, so the sweep stays allocation-free.
        /// </summary>
        private void PruneDestroyed()
        {
            _deadIds.Clear();

            foreach (KeyValuePair<int, Tracked> entry in _tracked)
            {
                if (entry.Value.Animator == null)
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
                    Animator animator = entry.Value.Animator;
                    if (animator != null && animator.cullingMode != entry.Value.OriginalMode)
                    {
                        animator.cullingMode = entry.Value.OriginalMode;
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
