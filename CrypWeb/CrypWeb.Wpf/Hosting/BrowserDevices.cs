using System.Collections.Generic;
using System.Threading;

namespace System.Windows.Hosting
{
    /// <summary>
    /// Devices of the browser (camera, microphone, speakers) for plugins that use hardware on the desktop (Webcam
    /// through Emgu CV, AudioInput/AudioOutput through NAudio). Plugin-side code asks for a device here; the app turns
    /// that into getUserMedia and Web Audio in the page (wwwroot/media.js) and delivers camera pictures and microphone
    /// samples back with <see cref="DeliverCameraFrame"/> and <see cref="DeliverMicrophoneSamples"/>.
    /// Audio is 16-bit mono PCM at <see cref="AudioSampleRate"/> samples per second (NAudio's default format).
    /// </summary>
    public static class BrowserDevices
    {
        public const int AudioSampleRate = 8000;

        private static readonly object AudioGate = new object();
        private static readonly List<Action<byte[]>> MicrophoneListeners = new List<Action<byte[]>>();

        /// <summary>Raised with true when the first listener needs the microphone, with false when the last one is gone.</summary>
        public static event Action<bool> MicrophoneNeeded;

        /// <summary>Raised with 16-bit PCM samples to play.</summary>
        public static event Action<byte[]> PlaybackRequested;

        /// <summary>Starts sending microphone samples to <paramref name="listener"/> (called on a background thread).</summary>
        public static void ListenToMicrophone(Action<byte[]> listener)
        {
            bool first;
            lock (AudioGate)
            {
                first = MicrophoneListeners.Count == 0;
                MicrophoneListeners.Add(listener);
            }
            if (first)
            {
                MicrophoneNeeded?.Invoke(true);
            }
        }

        public static void StopListening(Action<byte[]> listener)
        {
            bool last;
            lock (AudioGate)
            {
                if (!MicrophoneListeners.Remove(listener))
                {
                    return;
                }
                last = MicrophoneListeners.Count == 0;
            }
            if (last)
            {
                MicrophoneNeeded?.Invoke(false);
            }
        }

        /// <summary>16-bit PCM samples recorded by the page; handed to the listeners on a background thread.</summary>
        public static void DeliverMicrophoneSamples(byte[] samples)
        {
            Action<byte[]>[] listeners;
            lock (AudioGate)
            {
                listeners = MicrophoneListeners.ToArray();
            }
            if (listeners.Length > 0)
            {
                // plugins handle data on background threads, never on the UI thread that delivers it
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    foreach (Action<byte[]> listener in listeners)
                    {
                        listener(samples);
                    }
                });
            }
        }

        /// <summary>Plays 16-bit PCM samples on the page's speakers (after what is already queued).</summary>
        public static void Play(byte[] samples) => PlaybackRequested?.Invoke(samples);

        private static readonly object Gate = new object();
        private static int _cameraUsers;
        private static byte[] _cameraFrame;
        private static int _cameraFrameNumber;

        /// <summary>Raised with true when the first user starts the camera, with false when the last one stops it.</summary>
        public static event Action<bool> CameraNeeded;

        /// <summary>Starts using the camera (the page asks the user for permission on the first use).</summary>
        public static void StartCamera()
        {
            bool first;
            lock (Gate)
            {
                first = _cameraUsers++ == 0;
            }
            if (first)
            {
                CameraNeeded?.Invoke(true);
            }
        }

        public static void StopCamera()
        {
            bool last;
            lock (Gate)
            {
                if (_cameraUsers == 0)
                {
                    return;
                }
                last = --_cameraUsers == 0;
                if (last)
                {
                    _cameraFrame = null;
                }
            }
            if (last)
            {
                CameraNeeded?.Invoke(false);
            }
        }

        /// <summary>A picture from the camera (an encoded JPEG or PNG file), sent by the page.</summary>
        public static void DeliverCameraFrame(byte[] encodedPicture)
        {
            lock (Gate)
            {
                if (_cameraUsers > 0)
                {
                    _cameraFrame = encodedPicture;
                    _cameraFrameNumber++;
                    Monitor.PulseAll(Gate);
                }
            }
        }

        /// <summary>
        /// The newest camera picture, or null while none has arrived. Waits up to <paramref name="timeout"/> for the
        /// first one; on the UI thread pass zero (the page delivers pictures through the UI thread).
        /// </summary>
        public static byte[] LatestCameraFrame(TimeSpan timeout)
        {
            lock (Gate)
            {
                if (_cameraFrame == null && timeout > TimeSpan.Zero)
                {
                    Monitor.Wait(Gate, timeout);
                }
                return _cameraFrame;
            }
        }

        /// <summary>Counts delivered pictures (lets a reader see whether a new one arrived).</summary>
        public static int CameraFrameNumber
        {
            get
            {
                lock (Gate)
                {
                    return _cameraFrameNumber;
                }
            }
        }
    }
}
