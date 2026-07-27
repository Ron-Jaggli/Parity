using UnityEngine;

namespace Parity.Tweaks
{
    /// <summary>
    /// Widens the async upload path used to push streamed textures and meshes to
    /// the GPU.
    ///
    /// Unity's default ring buffer is 4 MB. When an asset does not fit, the upload
    /// is split across frames or falls back to a synchronous path, and you feel it
    /// as a hitch when walking into a new area. A larger, persistent buffer moves
    /// the same bytes in fewer, cleaner steps.
    ///
    /// Note what is deliberately not touched: <c>asyncUploadTimeSlice</c>, which
    /// would trade main-thread time per frame for faster uploads, and the texture
    /// streaming mip settings, which would change how sharp things look.
    /// </summary>
    internal sealed class StreamingTweak : Tweak
    {
        private const int MinBufferMb = 4;
        private const int MaxBufferMb = 128;

        public override string Name => "Streaming";

        private bool _captured;
        private int _originalBufferMb;
        private bool _originalPersistent;

        public override void Apply()
        {
            ParityLog.Try(Name + ".asyncUpload", () =>
            {
                if (!_captured)
                {
                    _originalBufferMb = QualitySettings.asyncUploadBufferSize;
                    _originalPersistent = QualitySettings.asyncUploadPersistentBuffer;
                    _captured = true;
                }

                bool on = ParityPreferences.TuneAsyncUploads.Value;

                int wantBuffer = _originalBufferMb;
                bool wantPersistent = _originalPersistent;

                if (on)
                {
                    int configured = Mathf.Clamp(
                        ParityPreferences.AsyncUploadBufferMb.Value, MinBufferMb, MaxBufferMb);

                    // Never shrink below what the game asked for.
                    wantBuffer = Mathf.Max(configured, _originalBufferMb);
                    wantPersistent = true;
                }

                // Assigning the buffer size reallocates it, so only write on a change.
                if (QualitySettings.asyncUploadBufferSize != wantBuffer)
                {
                    QualitySettings.asyncUploadBufferSize = wantBuffer;
                }

                if (QualitySettings.asyncUploadPersistentBuffer != wantPersistent)
                {
                    QualitySettings.asyncUploadPersistentBuffer = wantPersistent;
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
                if (QualitySettings.asyncUploadBufferSize != _originalBufferMb)
                {
                    QualitySettings.asyncUploadBufferSize = _originalBufferMb;
                }

                if (QualitySettings.asyncUploadPersistentBuffer != _originalPersistent)
                {
                    QualitySettings.asyncUploadPersistentBuffer = _originalPersistent;
                }
            });
        }
    }
}
