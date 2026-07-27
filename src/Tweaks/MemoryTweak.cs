using System.Diagnostics;
using UnityEngine;
using UnityEngine.Scripting;

namespace Parity.Tweaks
{
    /// <summary>
    /// Moves garbage collection off the critical path.
    ///
    /// The cost of a collection is not really the milliseconds it takes - it is
    /// when those milliseconds land. A 40 ms pause during a loading screen is
    /// invisible; the same pause mid-fight is a dropped frame you feel in your
    /// inner ear. So this deliberately provokes a collection while the loading
    /// screen is up, when the heap is at its dirtiest anyway, to reduce the chance
    /// that one lands during play.
    ///
    /// Both heaps are collected: the game runs on IL2CPP's collector, while
    /// MelonLoader and every code mod run on a separate .NET runtime with its own.
    /// </summary>
    internal sealed class MemoryTweak : Tweak
    {
        private const int MinSliceMicroseconds = 100;
        private const int MaxSliceMicroseconds = 8000;

        public override string Name => "Memory";

        private bool _captured;
        private uint _originalSliceNanoseconds;
        private bool _warnedNotIncremental;

        public override void Apply()
        {
            if (!ParityPreferences.TuneIncrementalGc.Value && !_captured)
            {
                return;
            }

            ParityLog.Try(Name + ".incrementalGc", () =>
            {
                if (!GarbageCollector.isIncremental)
                {
                    if (!_warnedNotIncremental && ParityPreferences.TuneIncrementalGc.Value)
                    {
                        _warnedNotIncremental = true;
                        ParityLog.Warn("Incremental GC is not enabled in this build, so " +
                                       "TuneIncrementalGc has nothing to adjust.");
                    }

                    return;
                }

                if (!_captured)
                {
                    _originalSliceNanoseconds = GarbageCollector.incrementalTimeSliceNanoseconds;
                    _captured = true;
                }

                uint want = _originalSliceNanoseconds;

                if (ParityPreferences.TuneIncrementalGc.Value)
                {
                    int microseconds = Mathf.Clamp(
                        ParityPreferences.IncrementalGcSliceMicroseconds.Value,
                        MinSliceMicroseconds,
                        MaxSliceMicroseconds);
                    want = (uint)microseconds * 1000u;
                }

                if (GarbageCollector.incrementalTimeSliceNanoseconds != want)
                {
                    GarbageCollector.incrementalTimeSliceNanoseconds = want;
                }
            });
        }

        public override void OnSceneLoaded(string sceneName)
        {
            bool collect = ParityPreferences.CollectOnSceneLoad.Value;
            bool unload = ParityPreferences.UnloadUnusedAssetsOnSceneLoad.Value;

            if (!collect && !unload)
            {
                return;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();

            if (collect)
            {
                // The game's heap.
                ParityLog.Try(Name + ".il2cppCollect", () => Il2CppSystem.GC.Collect());

                // MelonLoader's heap, where this mod and every other one lives.
                ParityLog.Try(Name + ".hostCollect", () => System.GC.Collect());
            }

            if (unload)
            {
                // Runs asynchronously, so it is not included in the timing below.
                // Collecting first means more assets have already lost their last
                // managed reference and therefore qualify as unused.
                ParityLog.Try(Name + ".unloadAssets", () => Resources.UnloadUnusedAssets());
            }

            stopwatch.Stop();
            ParityLog.Info("Reclaimed memory during load of '" + sceneName + "' in " +
                           stopwatch.ElapsedMilliseconds + " ms.");
        }

        public override void Revert()
        {
            if (!_captured)
            {
                return;
            }

            ParityLog.Try(Name + ".revert", () =>
            {
                if (GarbageCollector.isIncremental &&
                    GarbageCollector.incrementalTimeSliceNanoseconds != _originalSliceNanoseconds)
                {
                    GarbageCollector.incrementalTimeSliceNanoseconds = _originalSliceNanoseconds;
                }
            });
        }
    }
}
