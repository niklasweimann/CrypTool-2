// NAudio for the browser build: the classes the AudioInput and AudioOutput plugins use, connected to the page's
// microphone and speakers through System.Windows.Hosting.BrowserDevices (getUserMedia and Web Audio in media.js).
//
// The browser has one microphone and one output; device numbers are accepted and ignored. The audio format is
// NAudio's default for WaveIn: 8000 samples per second, 16 bit, mono.
using System;
using System.Windows.Hosting;

namespace NAudio.Wave
{
    public class WaveFormat
    {
        public WaveFormat() : this(BrowserDevices.AudioSampleRate, 16, 1) { }

        public WaveFormat(int rate, int bits, int channels)
        {
            SampleRate = rate;
            BitsPerSample = bits;
            Channels = channels;
        }

        public int SampleRate { get; }
        public int BitsPerSample { get; }
        public int Channels { get; }
        public int AverageBytesPerSecond => SampleRate * Channels * BitsPerSample / 8;
        public int BlockAlign => Channels * BitsPerSample / 8;
    }

    public interface IWaveProvider
    {
        WaveFormat WaveFormat { get; }
    }

    public class WaveInCapabilities
    {
        internal WaveInCapabilities(string productName) => ProductName = productName;

        public string ProductName { get; }

        public int Channels => 1;
    }

    public class WaveOutCapabilities
    {
        internal WaveOutCapabilities(string productName) => ProductName = productName;

        public string ProductName { get; }

        public int Channels => 2;
    }

    public static class WaveIn
    {
        public static int DeviceCount => 1;

        public static WaveInCapabilities GetCapabilities(int devNumber) => new WaveInCapabilities("Browser microphone");
    }

    public static class WaveOut
    {
        public static int DeviceCount => 1;

        public static WaveOutCapabilities GetCapabilities(int devNumber) => new WaveOutCapabilities("Browser audio output");
    }

    public class WaveInEventArgs : EventArgs
    {
        public WaveInEventArgs(byte[] buffer, int bytes)
        {
            Buffer = buffer;
            BytesRecorded = bytes;
        }

        public byte[] Buffer { get; }

        public int BytesRecorded { get; }
    }

    public class StoppedEventArgs : EventArgs
    {
        public StoppedEventArgs(Exception exception = null) => Exception = exception;

        public Exception Exception { get; }
    }

    /// <summary>Records from the microphone: <see cref="DataAvailable"/> receives the samples as they arrive.</summary>
    public class WaveInEvent : IDisposable
    {
        private bool _recording;

        public int DeviceNumber { get; set; }

        public WaveFormat WaveFormat { get; set; } = new WaveFormat();

        public int BufferMilliseconds { get; set; } = 100;

        public int NumberOfBuffers { get; set; } = 3;

        public event EventHandler<WaveInEventArgs> DataAvailable;

        public event EventHandler<StoppedEventArgs> RecordingStopped;

        public void StartRecording()
        {
            if (_recording)
            {
                return;
            }
            _recording = true;
            BrowserDevices.ListenToMicrophone(OnSamples);
        }

        public void StopRecording()
        {
            if (!_recording)
            {
                return;
            }
            _recording = false;
            BrowserDevices.StopListening(OnSamples);
            RecordingStopped?.Invoke(this, new StoppedEventArgs());
        }

        private void OnSamples(byte[] samples) => DataAvailable?.Invoke(this, new WaveInEventArgs(samples, samples.Length));

        public void Dispose() => StopRecording();
    }

    /// <summary>A queue of samples for a player; added samples are played in order.</summary>
    public class BufferedWaveProvider : IWaveProvider
    {
        public BufferedWaveProvider(WaveFormat waveFormat) => WaveFormat = waveFormat;

        public WaveFormat WaveFormat { get; }

        public TimeSpan BufferDuration { get; set; } = TimeSpan.FromSeconds(5);

        public bool DiscardOnBufferOverflow { get; set; }

        internal WaveOutEvent Player { get; set; }

        public void AddSamples(byte[] buffer, int offset, int count)
        {
            if (Player == null || !Player.IsPlaying || count <= 0)
            {
                return;
            }
            var samples = new byte[count];
            Array.Copy(buffer, offset, samples, 0, count);
            BrowserDevices.Play(samples);
        }

        public void ClearBuffer() { }
    }

    public enum PlaybackState
    {
        Stopped,
        Playing,
        Paused,
    }

    /// <summary>Plays a <see cref="BufferedWaveProvider"/> on the page's audio output.</summary>
    public class WaveOutEvent : IDisposable
    {
        private BufferedWaveProvider _provider;

        public int DeviceNumber { get; set; }

        public int DesiredLatency { get; set; } = 300;

        public float Volume { get; set; } = 1;

        public PlaybackState PlaybackState { get; private set; }

        internal bool IsPlaying => PlaybackState == PlaybackState.Playing;

        public event EventHandler<StoppedEventArgs> PlaybackStopped;

        public void Init(IWaveProvider waveProvider)
        {
            _provider = waveProvider as BufferedWaveProvider;
            if (_provider != null)
            {
                _provider.Player = this;
            }
        }

        public void Play() => PlaybackState = PlaybackState.Playing;

        public void Pause() => PlaybackState = PlaybackState.Paused;

        public void Stop()
        {
            if (PlaybackState == PlaybackState.Stopped)
            {
                return;
            }
            PlaybackState = PlaybackState.Stopped;
            PlaybackStopped?.Invoke(this, new StoppedEventArgs());
        }

        public void Dispose() => Stop();
    }
}
