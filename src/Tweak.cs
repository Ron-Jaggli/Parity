namespace Parity
{
    /// <summary>
    /// One self-contained optimisation.
    ///
    /// Tweaks are expected to be reversible. <see cref="Apply"/> is re-run
    /// periodically and after every scene load, so it must read its own preference
    /// and either apply the optimisation or put the engine back the way it found
    /// it. That is what makes toggling a setting in MelonPreferences take effect
    /// without a restart, and what makes the master switch meaningful.
    ///
    /// <see cref="Apply"/> is also expected to be idempotent and cheap: compare
    /// before you write, because some engine setters do real work (reallocating a
    /// buffer, for instance) even when handed the value they already hold.
    /// </summary>
    internal abstract class Tweak
    {
        /// <summary>Short name used in log lines and in exception-guard call sites.</summary>
        public abstract string Name { get; }

        /// <summary>
        /// Apply or un-apply this tweak's engine settings to match its preference.
        /// Called at startup, after every scene load, and on a slow timer.
        /// </summary>
        public virtual void Apply()
        {
        }

        /// <summary>
        /// One-shot work for a freshly loaded scene. Runs while the loading screen
        /// is still up, which is the only safe moment for anything expensive.
        /// </summary>
        public virtual void OnSceneLoaded(string sceneName)
        {
        }

        /// <summary>
        /// Per-frame work. Keep it bounded: whatever happens here is on the critical
        /// path of a VR frame.
        /// </summary>
        public virtual void Tick(float now)
        {
        }

        /// <summary>
        /// Unconditionally restore vanilla behaviour, regardless of preferences.
        /// Used by the master switch.
        /// </summary>
        public virtual void Revert()
        {
        }
    }
}
