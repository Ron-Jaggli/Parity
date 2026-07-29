// Reference assembly. Signatures mirror Unity 2021.3's UnityEngine.PhysicsModule.
// See refs/README.md before editing.
namespace UnityEngine
{
    public class Physics
    {
        public static bool reuseCollisionCallbacks { get; set; }

        public static bool autoSyncTransforms { get; set; }

        /// <summary>Global on/off for cloth inter-collision.</summary>
        public static bool interCollisionSettingsToggle { get; set; }
    }
}
