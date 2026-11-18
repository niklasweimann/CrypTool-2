using Microsoft.JSInterop;
using LogLevel = CrypTool.Web.Core.LogLevel;

namespace CrypWeb.App.Pages;

/// <summary>
///     "Make available offline" (View tab): opens the offline dialog (Components/OfflineModal.razor), where the user
///     chooses what to keep on this device - the templates, the pictures, the language data of single languages - and
///     downloads, refreshes or deletes it (wwwroot/offline.js). The download runs in the service worker and belongs to
///     this page, so it goes on when the dialog is closed; its progress shows in the status bar.
///     Without it, whatever was used once is cached anyway.
/// </summary>
public partial class Home
{
    private bool offlineOpen;

    /// <summary>The running download, or null.</summary>
    private OfflineRun? offlineRun;

    /// <summary>Counts finished downloads: the dialog then reads the state of the cache again.</summary>
    private int offlineVersion;

    private void MakeAvailableOffline()
    {
        offlineOpen = true;
    }

    private async Task StartOfflineDownload((string[] Groups, bool Fresh) request)
    {
        if (offlineRun != null || request.Groups.Length == 0) return;
        offlineRun = new OfflineRun(0, 0, 0, 0, 0);
        var started = false;
        try
        {
            started = await Js.InvokeAsync<bool>("ctOffline.download", request.Groups, request.Fresh, self);
        }
        catch (JSException)
        {
            /* no service worker in this browser */
        }

        if (started) return;
        offlineRun = null;
        AddLog(Loc["log.workspace"], LogLevel.Warning, Loc["offline.unavailable"]);
    }

    private async Task CancelOfflineDownload()
    {
        await Js.InvokeVoidAsync("ctOffline.cancel");
    }

    [JSInvokable]
    public void OnOfflineProgress(int done, int failed, int total, long bytes, long totalBytes, bool finished, bool cancelled)
    {
        if (!finished)
        {
            offlineRun = new OfflineRun(done, failed, total, bytes, totalBytes);
        }
        else
        {
            offlineRun = null;
            offlineVersion++;
            var message = cancelled ? Loc.T("offline.cancelled", done - failed, total) : Loc.T("offline.done", done - failed, total);
            AddLog(Loc["log.workspace"], failed > 0 && !cancelled ? LogLevel.Warning : LogLevel.Info, message);
        }

        StateHasChanged();
    }
}

/// <summary>Progress of a download for offline use: files done (failed among them) of total, and bytes.</summary>
public sealed record OfflineRun(int Done, int Failed, int Total, long Bytes, long TotalBytes);
