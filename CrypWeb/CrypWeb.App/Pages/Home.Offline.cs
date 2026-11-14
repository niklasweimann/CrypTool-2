using Microsoft.JSInterop;
using LogLevel = CrypTool.Web.Core.LogLevel;

namespace CrypWeb.App.Pages;

/// <summary>
///     "Make available offline" (View tab): the service worker (wwwroot/service-worker.js) downloads all templates and
///     the data files of the plugins in advance, so that every template also runs without a network connection.
///     Without it, whatever was used once is cached anyway.
/// </summary>
public partial class Home
{
    /// <summary>Progress of the download: null while none is running.</summary>
    private (int Done, int Total)? precache;

    private async Task MakeAvailableOffline()
    {
        if (precache != null) return;
        var started = false;
        try
        {
            started = await Js.InvokeAsync<bool>("ct2.precacheAll", self);
        }
        catch (JSException)
        {
            /* no service worker in this browser */
        }

        if (started) precache = (0, 0); // the list of files is being put together
        else AddLog(Loc["log.workspace"], LogLevel.Warning, Loc["offline.unavailable"]);
    }

    [JSInvokable]
    public void OnPrecacheProgress(int done, int failed, int total, bool finished)
    {
        precache = finished ? null : (done, total);
        // two templates have no text file (.xml): those requests fail and are not worth a warning
        if (finished) AddLog(Loc["log.workspace"], LogLevel.Info, Loc.T("offline.done", done - failed, total));
        StateHasChanged();
    }
}
