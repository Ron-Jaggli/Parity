using UnityEngine.XR;

namespace Parity.Tweaks
{
    /// <summary>
    /// Turns on the headset's occlusion mesh.
    ///
    /// A VR lens is round; the eye render target is rectangular. The corners of that
    /// target are shaded every frame and then thrown away, because the optics cannot
    /// route them to your eye. The occlusion mesh is a stencil the runtime supplies
    /// that masks those pixels off before shading, typically reclaiming ~10-17% of
    /// fragment work depending on the headset.
    ///
    /// This is the purest example of what this mod is for: strictly less GPU work,
    /// and the pixels it skips are ones no human has ever seen.
    /// </summary>
    internal sealed class VrRenderTweak : Tweak
    {
        public override string Name => "VrRender";

        private bool _captured;
        private bool _original;
        private bool _announced;

        public override void Apply()
        {
            ParityLog.Try(Name + ".occlusionMesh", () =>
            {
                // Flat-screen or headset-less launches have nothing to mask.
                if (!XRSettings.enabled)
                {
                    return;
                }

                if (!_captured)
                {
                    _original = XRSettings.useOcclusionMesh;
                    _captured = true;

                    if (_original && !_announced)
                    {
                        _announced = true;
                        ParityLog.Info("VR occlusion mesh was already enabled by the game - nothing to gain here.");
                    }
                }

                bool want = ParityPreferences.UseOcclusionMesh.Value || _original;
                if (XRSettings.useOcclusionMesh != want)
                {
                    XRSettings.useOcclusionMesh = want;
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
                if (XRSettings.useOcclusionMesh != _original)
                {
                    XRSettings.useOcclusionMesh = _original;
                }
            });
        }
    }
}
