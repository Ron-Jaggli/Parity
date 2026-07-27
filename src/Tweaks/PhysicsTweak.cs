using UnityEngine;

namespace Parity.Tweaks
{
    /// <summary>
    /// Three PhysX settings, in descending order of how safe they are.
    ///
    /// <c>reuseCollisionCallbacks</c> is the one that matters here. BONELAB is a
    /// physics sandbox: crates, limbs and ragdolls generate a constant stream of
    /// collision callbacks, and by default each one allocates a fresh Collision
    /// object. That garbage is why physics-heavy moments end in a GC pause. Reusing
    /// the object removes the allocation outright. It is only unsafe for code that
    /// stashes a Collision reference to read on a later frame, which is a documented
    /// mistake rather than a normal pattern.
    ///
    /// The other two ship disabled and are described in the README.
    /// </summary>
    internal sealed class PhysicsTweak : Tweak
    {
        public override string Name => "Physics";

        private bool _captured;
        private bool _originalReuseCallbacks;
        private bool _originalAutoSync;
        private bool _originalInterCollision;

        public override void Apply()
        {
            ParityLog.Try(Name + ".settings", () =>
            {
                if (!_captured)
                {
                    _originalReuseCallbacks = Physics.reuseCollisionCallbacks;
                    _originalAutoSync = Physics.autoSyncTransforms;
                    _originalInterCollision = Physics.interCollisionSettingsToggle;
                    _captured = true;
                }

                bool wantReuse = ParityPreferences.ReuseCollisionCallbacks.Value || _originalReuseCallbacks;
                if (Physics.reuseCollisionCallbacks != wantReuse)
                {
                    Physics.reuseCollisionCallbacks = wantReuse;
                }

                bool wantAutoSync = ParityPreferences.DisableAutoSyncTransforms.Value
                    ? false
                    : _originalAutoSync;
                if (Physics.autoSyncTransforms != wantAutoSync)
                {
                    Physics.autoSyncTransforms = wantAutoSync;
                }

                bool wantInterCollision = ParityPreferences.DisableClothInterCollision.Value
                    ? false
                    : _originalInterCollision;
                if (Physics.interCollisionSettingsToggle != wantInterCollision)
                {
                    Physics.interCollisionSettingsToggle = wantInterCollision;
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
                if (Physics.reuseCollisionCallbacks != _originalReuseCallbacks)
                {
                    Physics.reuseCollisionCallbacks = _originalReuseCallbacks;
                }

                if (Physics.autoSyncTransforms != _originalAutoSync)
                {
                    Physics.autoSyncTransforms = _originalAutoSync;
                }

                if (Physics.interCollisionSettingsToggle != _originalInterCollision)
                {
                    Physics.interCollisionSettingsToggle = _originalInterCollision;
                }
            });
        }
    }
}
