// Reference assembly. Signatures mirror Unity 2021.3's UnityEngine.AnimationModule.
// See refs/README.md before editing.
using System;

namespace UnityEngine
{
    // These values are baked into the consuming assembly's IL, so getting one
    // wrong would silently select a different culling mode with no error anywhere.
    public enum AnimatorCullingMode
    {
        AlwaysAnimate = 0,
        CullUpdateTransforms = 1,
        CullCompletely = 2,
    }

    public class RuntimeAnimatorController : Object
    {
        public RuntimeAnimatorController(IntPtr pointer) : base(pointer)
        {
        }
    }

    public class Animator : Behaviour
    {
        public Animator(IntPtr pointer) : base(pointer)
        {
        }

        public AnimatorCullingMode cullingMode { get; set; }

        public bool applyRootMotion { get; set; }

        public RuntimeAnimatorController runtimeAnimatorController { get; set; }
    }
}
