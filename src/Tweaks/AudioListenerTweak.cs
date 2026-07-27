using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace Parity.Tweaks
{
    /// <summary>
    /// Disables surplus audio listeners.
    ///
    /// Unity supports exactly one active listener. When a scene ends up with
    /// several - usually because a spawned prefab brought its own - Unity picks one
    /// arbitrarily, warns about it, and the others still cost per-frame work while
    /// contributing nothing.
    ///
    /// This one ships disabled, and it declines to act unless it can positively
    /// identify which listener to keep. Guessing wrong here does not cost frames,
    /// it makes the game silent, and that is not a trade worth making automatically.
    /// </summary>
    internal sealed class AudioListenerTweak : Tweak
    {
        public override string Name => "AudioListener";

        private readonly List<AudioListener> _disabled = new List<AudioListener>();
        private bool _active;

        public override void OnSceneLoaded(string sceneName)
        {
            _disabled.Clear();

            if (!ParityPreferences.RemoveDuplicateAudioListeners.Value)
            {
                return;
            }

            _active = true;
            ParityLog.Try(Name + ".prune", Prune);
        }

        public override void Tick(float now)
        {
            if (_active && !ParityPreferences.RemoveDuplicateAudioListeners.Value)
            {
                Revert();
            }
        }

        private void Prune()
        {
            Il2CppReferenceArray<AudioListener> listeners =
                UnityEngine.Object.FindObjectsOfType<AudioListener>();

            if (listeners.Length <= 1)
            {
                return;
            }

            AudioListener keep = ChoosePreferred(listeners);
            if (keep == null)
            {
                ParityLog.Warn(listeners.Length + " audio listeners are active but none is on a " +
                               "camera, so it is not clear which one the game intends to use. " +
                               "Leaving them alone.");
                return;
            }

            for (int i = 0; i < listeners.Length; i++)
            {
                AudioListener listener = listeners[i];
                if (listener == null || listener == keep || !listener.enabled)
                {
                    continue;
                }

                listener.enabled = false;
                _disabled.Add(listener);
            }

            if (_disabled.Count > 0)
            {
                ParityLog.Info("Disabled " + _disabled.Count + " surplus audio listener" +
                               (_disabled.Count == 1 ? "" : "s") + ", keeping the one on '" +
                               keep.gameObject.name + "'.");
            }
        }

        private static AudioListener ChoosePreferred(Il2CppReferenceArray<AudioListener> listeners)
        {
            Camera main = Camera.main;

            if (main != null)
            {
                for (int i = 0; i < listeners.Length; i++)
                {
                    AudioListener listener = listeners[i];
                    if (listener != null && listener.gameObject == main.gameObject)
                    {
                        return listener;
                    }
                }
            }

            // No listener on the main camera. Fall back to one sitting on any live
            // camera, which is still a defensible choice.
            for (int i = 0; i < listeners.Length; i++)
            {
                AudioListener listener = listeners[i];
                if (listener == null)
                {
                    continue;
                }

                Camera camera = listener.GetComponent<Camera>();
                if (camera != null && camera.isActiveAndEnabled)
                {
                    return listener;
                }
            }

            return null;
        }

        public override void Revert()
        {
            ParityLog.Try(Name + ".revert", () =>
            {
                for (int i = 0; i < _disabled.Count; i++)
                {
                    AudioListener listener = _disabled[i];
                    if (listener != null)
                    {
                        listener.enabled = true;
                    }
                }
            });

            _disabled.Clear();
            _active = false;
        }
    }
}
