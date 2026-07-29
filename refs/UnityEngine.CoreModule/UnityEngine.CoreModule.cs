// Reference assembly. Signatures mirror Unity 2021.3's UnityEngine.CoreModule.
// See refs/README.md before editing.
using System;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace UnityEngine
{
    // Values are baked into the IL of the consuming assembly, so they are written
    // out explicitly rather than left to declaration order.
    public enum LogType
    {
        Error = 0,
        Assert = 1,
        Warning = 2,
        Log = 3,
        Exception = 4,
    }

    public enum StackTraceLogType
    {
        None = 0,
        ScriptOnly = 1,
        Full = 2,
    }

    public interface ILogger
    {
        bool logEnabled { get; set; }
    }

    public class Debug
    {
        public static ILogger unityLogger => null;
    }

    public class Application
    {
        public static StackTraceLogType GetStackTraceLogType(LogType logType) => StackTraceLogType.None;

        public static void SetStackTraceLogType(LogType logType, StackTraceLogType stackTraceType)
        {
        }
    }

    public sealed class QualitySettings
    {
        public static int asyncUploadBufferSize { get; set; }

        public static bool asyncUploadPersistentBuffer { get; set; }
    }

    public class Time
    {
        public static float maximumDeltaTime { get; set; }

        public static float fixedDeltaTime { get; set; }

        public static float unscaledDeltaTime => 0f;

        public static float realtimeSinceStartup => 0f;
    }

    // Mathf really is a struct in Unity, not a static class.
    public struct Mathf
    {
        public static int Clamp(int value, int min, int max) => value;

        public static float Clamp(float value, float min, float max) => value;

        public static int Max(int a, int b) => a;

        public static float Max(float a, float b) => a;

        public static bool Approximately(float a, float b) => false;

        public static int CeilToInt(float f) => 0;
    }

    public class Object : Il2CppObjectBase
    {
        public Object(IntPtr pointer) : base(pointer)
        {
        }

        public string name { get; set; }

        public int GetInstanceID() => 0;

        public static Il2CppReferenceArray<T> FindObjectsOfType<T>() where T : Object => null;

        public static bool operator ==(Object x, Object y) => ReferenceEquals(x, y);

        public static bool operator !=(Object x, Object y) => !ReferenceEquals(x, y);
    }

    public class AsyncOperation
    {
    }

    public class Resources
    {
        public static AsyncOperation UnloadUnusedAssets() => null;
    }

    public class Component : Object
    {
        public Component(IntPtr pointer) : base(pointer)
        {
        }

        public Transform transform => null;

        public GameObject gameObject => null;

        public T GetComponent<T>() where T : Component => null;
    }

    public class Transform : Component
    {
        public Transform(IntPtr pointer) : base(pointer)
        {
        }

        public Transform root => null;
    }

    public sealed class GameObject : Object
    {
        public GameObject(IntPtr pointer) : base(pointer)
        {
        }
    }

    public class Behaviour : Component
    {
        public Behaviour(IntPtr pointer) : base(pointer)
        {
        }

        public bool enabled { get; set; }

        public bool isActiveAndEnabled => false;
    }

    public sealed class Camera : Behaviour
    {
        public Camera(IntPtr pointer) : base(pointer)
        {
        }

        public static Camera main => null;
    }
}

namespace UnityEngine.Scripting
{
    public static class GarbageCollector
    {
        public static bool isIncremental => false;

        public static uint incrementalTimeSliceNanoseconds { get; set; }
    }
}
