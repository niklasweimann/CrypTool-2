using System.Windows.Hosting;
using Microsoft.JSInterop;

namespace CrypWeb.App.Engine;

/// <summary>
///     Connects the browser's devices to plugins (System.Windows.Hosting.BrowserDevices): when a plugin opens the camera
///     or the microphone, the page starts it with getUserMedia (wwwroot/media.js) and sends pictures and samples back
///     through <see cref="CameraFrame" /> and <see cref="MicrophoneSamples" />; samples to play go to the page's speakers.
/// </summary>
public static class MediaBridge
{
    private static IJSInProcessRuntime? Js { get; set; }
    private static SynchronizationContext? Ui { get; set; }

    /// <summary>Called once on the UI thread when the page has started.</summary>
    public static void Initialize(IJSRuntime runtime)
    {
        Js = runtime as IJSInProcessRuntime;
        Ui = SynchronizationContext.Current;
        BrowserDevices.CameraNeeded += on =>
        {
            // plugins start the camera on any thread; the page can only be called on the UI thread
            OnUi(() => SetCamera(on));
        };
        BrowserDevices.MicrophoneNeeded += on => OnUi(() => Call("ct2media.microphone",
            on,
            BrowserDevices.AudioSampleRate));
        BrowserDevices.PlaybackRequested += samples =>
        {
            var encoded = Convert.ToBase64String(samples);
            OnUi(() => Call("ct2media.play", encoded, BrowserDevices.AudioSampleRate));
        };
    }

    private static void OnUi(Action action)
    {
        if (Ui == null || SynchronizationContext.Current == Ui) action();
        else Ui.Post(_ => action(), null);
    }

    private static void SetCamera(bool on)
    {
        try
        {
            Js?.InvokeVoid("ct2media.camera", on);
        }
        catch (JSException)
        {
            /* media access is optional: without it the component just gets no data */
        }
    }

    private static void Call(string function, params object[] arguments)
    {
        try
        {
            Js?.InvokeVoid(function, arguments);
        }
        catch (JSException)
        {
            /* media access is optional: without it the component just gets no data */
        }
    }

    /// <summary>Microphone samples from the page: 16-bit mono PCM, base64.</summary>
    [JSInvokable]
    public static void MicrophoneSamples(string base64)
    {
        BrowserDevices.DeliverMicrophoneSamples(Convert.FromBase64String(base64));
    }

    /// <summary>A camera picture from the page, as a data URL (JPEG).</summary>
    [JSInvokable]
    public static void CameraFrame(string dataUrl)
    {
        var comma = dataUrl.IndexOf(',');
        if (comma < 0) return;
        BrowserDevices.DeliverCameraFrame(Convert.FromBase64String(dataUrl[(comma + 1)..]));
    }
}
