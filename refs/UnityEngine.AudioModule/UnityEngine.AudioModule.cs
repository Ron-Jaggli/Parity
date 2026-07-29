// Reference assembly. Signatures mirror Unity 2021.3's UnityEngine.AudioModule.
// See refs/README.md before editing.
using System;

namespace UnityEngine
{
    public sealed class AudioListener : Behaviour
    {
        public AudioListener(IntPtr pointer) : base(pointer)
        {
        }
    }
}
