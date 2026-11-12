using Microsoft.JSInterop;

namespace CrypWeb.App.Engine;

/// <summary>
/// Connects the browser's devices to plugins (System.Windows.Hosting.BrowserDevices): when a plugin opens the camera
/// or the microphone, the page starts it with getUserMedia (wwwroot/media.js) and sends pictures and samples back
/// through <see cref="CameraFrame"/> and <see cref="MicrophoneSamples"/>; samples to play go to the page's speakers.
/// </summary>
public static class MediaBridge
{
    static IJSInProcessRuntime? js;
    static SynchronizationContext? ui;

    /// <summary>Called once on the UI thread when the page has started.</summary>
    public static void Initialize(IJSRuntime runtime)
    {
        js = runtime as IJSInProcessRuntime;
        ui = SynchronizationContext.Current;
        System.Windows.Hosting.BrowserDevices.CameraNeeded += on =>
        {
            // plugins start the camera on any thread; the page can only be called on the UI thread
            OnUi(() => SetCamera(on));
        };
        System.Windows.Hosting.BrowserDevices.MicrophoneNeeded += on => OnUi(() => Call("ct2media.microphone", on, System.Windows.Hosting.BrowserDevices.AudioSampleRate));
        System.Windows.Hosting.BrowserDevices.PlaybackRequested += samples =>
        {
            string encoded = Convert.ToBase64String(samples);
            OnUi(() => Call("ct2media.play", encoded, System.Windows.Hosting.BrowserDevices.AudioSampleRate));
        };
    }

    static void OnUi(Action action)
    {
        if (ui == null || SynchronizationContext.Current == ui) action();
        else ui.Post(_ => action(), null);
    }

    static void SetCamera(bool on)
    {
        try { js?.InvokeVoid("ct2media.camera", on); }
        catch (JSException) { }
    }

    static void Call(string function, params object[] arguments)
    {
        try { js?.InvokeVoid(function, arguments); }
        catch (JSException) { }
    }

    /// <summary>Microphone samples from the page: 16-bit mono PCM, base64.</summary>
    [JSInvokable]
    public static void MicrophoneSamples(string base64) =>
        System.Windows.Hosting.BrowserDevices.DeliverMicrophoneSamples(Convert.FromBase64String(base64));

    /// <summary>A camera picture from the page, as a data URL (JPEG).</summary>
    [JSInvokable]
    public static void CameraFrame(string dataUrl)
    {
        int comma = dataUrl.IndexOf(',');
        if (comma < 0) return;
        System.Windows.Hosting.BrowserDevices.DeliverCameraFrame(Convert.FromBase64String(dataUrl[(comma + 1)..]));
    }
}
