using MelonLoader;

namespace Parity
{
    /// <summary>
    /// Every knob this mod exposes, written to
    /// <c>UserData/MelonPreferences.cfg</c> in the loader's data folder on first
    /// run. See the README for where that lands on a headset.
    ///
    /// Defaults are chosen so that enabling the mod is visually indistinguishable
    /// from vanilla. Anything that could conceivably alter a pixel, a sound, or a
    /// gameplay outcome ships disabled and is called out in the README.
    /// </summary>
    internal static class ParityPreferences
    {
        private const string CategoryId = "Parity";

        public static MelonPreferences_Category Category { get; private set; }

        // Master switch ------------------------------------------------------
        public static MelonPreferences_Entry<bool> Enabled { get; private set; }

        // Logging ------------------------------------------------------------
        public static MelonPreferences_Entry<bool> StripLogStackTraces { get; private set; }

        // VR render ----------------------------------------------------------
        public static MelonPreferences_Entry<bool> UseOcclusionMesh { get; private set; }

        // Asset streaming ----------------------------------------------------
        public static MelonPreferences_Entry<bool> TuneAsyncUploads { get; private set; }
        public static MelonPreferences_Entry<int> AsyncUploadBufferMb { get; private set; }

        // Frame pacing -------------------------------------------------------
        public static MelonPreferences_Entry<bool> ClampMaximumDeltaTime { get; private set; }
        public static MelonPreferences_Entry<int> MaxCatchUpPhysicsSteps { get; private set; }

        // Physics ------------------------------------------------------------
        public static MelonPreferences_Entry<bool> ReuseCollisionCallbacks { get; private set; }
        public static MelonPreferences_Entry<bool> DisableAutoSyncTransforms { get; private set; }
        public static MelonPreferences_Entry<bool> DisableClothInterCollision { get; private set; }

        // Animation ----------------------------------------------------------
        public static MelonPreferences_Entry<bool> CullOffscreenAnimators { get; private set; }
        public static MelonPreferences_Entry<bool> AggressiveAnimatorCulling { get; private set; }
        public static MelonPreferences_Entry<float> AnimatorRescanSeconds { get; private set; }
        public static MelonPreferences_Entry<int> AnimatorsPerFrame { get; private set; }
        public static MelonPreferences_Entry<string> AnimatorExcludeNames { get; private set; }

        // Audio --------------------------------------------------------------
        public static MelonPreferences_Entry<bool> RemoveDuplicateAudioListeners { get; private set; }

        // Memory -------------------------------------------------------------
        public static MelonPreferences_Entry<bool> CollectOnSceneLoad { get; private set; }
        public static MelonPreferences_Entry<bool> UnloadUnusedAssetsOnSceneLoad { get; private set; }
        public static MelonPreferences_Entry<bool> TuneIncrementalGc { get; private set; }
        public static MelonPreferences_Entry<int> IncrementalGcSliceMicroseconds { get; private set; }

        // Telemetry ----------------------------------------------------------
        public static MelonPreferences_Entry<float> FrameReportSeconds { get; private set; }

        public static void Register()
        {
            Category = MelonPreferences.CreateCategory(CategoryId, "Parity");

            Enabled = Category.CreateEntry(
                "Enabled", true, "Enabled",
                "Master switch. Turn off to make the mod inert without uninstalling it. " +
                "Takes effect on the next scene load.");

            // -- Logging --
            StripLogStackTraces = Category.CreateEntry(
                "StripLogStackTraces", true, "Strip Log Stack Traces",
                "Stop Unity capturing a managed stack trace for every Debug.Log and " +
                "Debug.LogWarning. Errors, asserts and exceptions keep their traces so " +
                "crash reports stay useful. Purely a CPU saving - no visual change.");

            // -- VR render --
            UseOcclusionMesh = Category.CreateEntry(
                "UseOcclusionMesh", true, "Use VR Occlusion Mesh",
                "Enable the headset's stencil mask so the GPU skips shading pixels that sit " +
                "outside the lens area and can never be seen. Invisible by construction.");

            // -- Asset streaming --
            TuneAsyncUploads = Category.CreateEntry(
                "TuneAsyncUploads", true, "Tune Async Texture Uploads",
                "Grow the async upload ring buffer and keep it resident so streaming large " +
                "textures and meshes causes fewer hitches. Same assets, same appearance.");

            AsyncUploadBufferMb = Category.CreateEntry(
                "AsyncUploadBufferMb", 8, "Async Upload Buffer (MB)",
                "Size of the async upload ring buffer. Unity's default is 4. The default " +
                "here is deliberately modest because the buffer stays resident for the " +
                "whole session and a headset has far less memory to spare than a PC - " +
                "spending it here means not spending it on textures. Raise it if you still " +
                "hitch walking into new areas. Clamped to 4-128.");

            // -- Frame pacing --
            ClampMaximumDeltaTime = Category.CreateEntry(
                "ClampMaximumDeltaTime", true, "Clamp Maximum Delta Time",
                "Cap how much simulation Unity tries to catch up on after a hitch. Without " +
                "this, one long frame queues a pile of physics steps, which causes the next " +
                "long frame, and the stutter feeds itself. Costs a few ms of wall-clock time " +
                "drift during a hitch; nothing renders differently.");

            MaxCatchUpPhysicsSteps = Category.CreateEntry(
                "MaxCatchUpPhysicsSteps", 3, "Max Catch-Up Physics Steps",
                "How many fixed timesteps a single frame may run. Lower is smoother but " +
                "drifts further from real time under load. Clamped to 2-10.");

            // -- Physics --
            ReuseCollisionCallbacks = Category.CreateEntry(
                "ReuseCollisionCallbacks", true, "Reuse Collision Callbacks",
                "Reuse one Collision object across collision callbacks instead of allocating " +
                "a fresh one per contact. Large reduction in garbage during physics-heavy " +
                "moments, which means fewer GC pauses.");

            DisableAutoSyncTransforms = Category.CreateEntry(
                "DisableAutoSyncTransforms", false, "Disable Physics Auto Sync Transforms",
                "Stop PhysX re-syncing every moved transform before each raycast. A real win " +
                "in scenes with many queries, but code that moves a transform and immediately " +
                "raycasts against it can observe stale positions. Off by default - try it, " +
                "and turn it back off if anything feels wrong.");

            DisableClothInterCollision = Category.CreateEntry(
                "DisableClothInterCollision", false, "Disable Cloth Inter-Collision",
                "Stop cloth simulations colliding with each other. Cheap win where it is " +
                "unused, but if any garment relies on it you would see cloth pass through " +
                "cloth. Off by default because it is one of the few settings here that can " +
                "visibly change something.");

            // -- Animation --
            CullOffscreenAnimators = Category.CreateEntry(
                "CullOffscreenAnimators", true, "Cull Offscreen Animators",
                "Skip transform writes, IK and retargeting for animators whose renderers are " +
                "offscreen. State machines still tick and animation events still fire, so " +
                "gameplay is unaffected, and nothing you can see stops moving. Animators " +
                "using root motion are left alone.");

            AggressiveAnimatorCulling = Category.CreateEntry(
                "AggressiveAnimatorCulling", false, "Aggressive Animator Culling",
                "Use CullCompletely instead of CullUpdateTransforms: offscreen animators stop " +
                "evaluating at all. Faster, but animation events stop firing while culled, " +
                "which can stall scripted behaviour. Off by default.");

            AnimatorRescanSeconds = Category.CreateEntry(
                "AnimatorRescanSeconds", 30f, "Animator Rescan Interval (s)",
                "How often to look for animators spawned since the last sweep. Finding every " +
                "animator in a scene is itself not cheap on a mobile CPU, so this is spaced " +
                "out further than a PC would need. Clamped to 2-300 seconds. Each sweep is " +
                "spread over many frames.");

            AnimatorsPerFrame = Category.CreateEntry(
                "AnimatorsPerFrame", 16, "Animators Per Frame",
                "Work budget for the sweep, so the optimiser never becomes the stutter. " +
                "Kept low for a mobile CPU: a sweep that takes a few more frames costs " +
                "nothing, a sweep that drops one costs a lot. Clamped to 4-512.");

            AnimatorExcludeNames = Category.CreateEntry(
                "AnimatorExcludeNames",
                "RigManager,PhysicsRig,ControllerRig,OpenControllerRig,RemapRig,ArtRig",
                "Animator Exclusions",
                "Comma-separated substrings. An animator is left alone if this matches " +
                "either its own name or the name of its hierarchy root, case-insensitively. " +
                "The defaults cover the player rig, whose pose is read by grab and " +
                "locomotion code even on frames where no camera can see it. Add an entry " +
                "here if a modded avatar or NPC misbehaves.");

            // -- Audio --
            RemoveDuplicateAudioListeners = Category.CreateEntry(
                "RemoveDuplicateAudioListeners", false, "Remove Duplicate Audio Listeners",
                "Unity picks one listener arbitrarily when several are active and wastes work " +
                "on the rest. This disables the extras, preferring to keep the one on the main " +
                "camera. Off by default: if the mod guesses wrong the game goes quiet, and " +
                "that is a bad trade for a small saving.");

            // -- Memory --
            CollectOnSceneLoad = Category.CreateEntry(
                "CollectOnSceneLoad", true, "Collect Garbage On Scene Load",
                "Force a collection while the loading screen is up, so that garbage is not " +
                "collected mid-fight instead. Never runs during gameplay.");

            UnloadUnusedAssetsOnSceneLoad = Category.CreateEntry(
                "UnloadUnusedAssetsOnSceneLoad", true, "Unload Unused Assets On Scene Load",
                "Release textures and meshes nothing references any more, during the loading " +
                "screen. Lower memory pressure means less streaming churn later.");

            TuneIncrementalGc = Category.CreateEntry(
                "TuneIncrementalGc", false, "Tune Incremental GC Slice",
                "Override how long the incremental collector may run per frame. Only does " +
                "anything if the game shipped with incremental GC enabled. Off by default " +
                "because the shipped value is usually already sensible.");

            IncrementalGcSliceMicroseconds = Category.CreateEntry(
                "IncrementalGcSliceMicroseconds", 2000, "Incremental GC Slice (us)",
                "Per-frame budget for the incremental collector, in microseconds. Clamped to " +
                "100-8000.");

            // -- Telemetry --
            FrameReportSeconds = Category.CreateEntry(
                "FrameReportSeconds", 120f, "Frame Time Report Interval (s)",
                "Write a frame time summary to the log this often, so you can measure " +
                "whether any of this helped. On a headset there is no console, so this lands " +
                "in MelonLoader's log file - see the README. Set to 0 to disable. Nothing is " +
                "ever drawn in the headset.");

            Category.SaveToFile(false);
        }
    }
}
