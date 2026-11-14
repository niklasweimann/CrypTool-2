using System.Text;
using CrypTool.Web.Core;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace CrypWeb.App.Pages;

/// <summary>Opening templates and files, adding images.</summary>
public partial class Home
{
    private const long MaxWorkspaceFileBytes = 50_000_000;
    private const long MaxImageFileBytes = 20_000_000;

    /// <summary>The data URIs of the pictures; building one (base64 of megabytes) on every render made dragging sluggish.</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<byte[], string> ImageUris = new();

    private bool ActiveIsBlank =>
        active.Kind == DocKind.Workspace
        && current == null
        && !ed.Dirty
        && ed.Ws.Plugins.Count + ed.Ws.Texts.Count + ed.Ws.Images.Count == 0;

    /// <summary>Makes sure there is a workspace tab to load into: reuses an empty untitled one, otherwise opens a new tab.</summary>
    private void EnsureTarget()
    {
        if (ActiveIsBlank) return;
        var doc = new Doc();
        docs.Add(doc);
        active = doc;
        ed.Load(new Workspace());
    }

    /// <summary>URL of a file below <c>templates/</c>; every path segment is escaped separately.</summary>
    private static string TemplateUrl(string relativePath)
    {
        return "templates/" + string.Join("/", relativePath.Split('/').Select(Uri.EscapeDataString));
    }

    /// <summary>
    ///     Loads a template (<c>.cwm</c>) and, if present, its <c>.xml</c> with the localized texts for the
    ///     <c>$placeholders$</c>. With <paramref name="inPlace" /> the current tab is replaced instead of opening a new one.
    /// </summary>
    /// <param name="adjust">Changes the workspace before it is shown (settings of a link); such a template always gets a tab of its own.</param>
    private async Task OpenTemplate(string template, bool inPlace = false, Action<Workspace>? adjust = null)
    {
        if (!inPlace)
        {
            var alreadyOpen = adjust != null ? null : docs.FirstOrDefault(d => d.Kind == DocKind.Workspace && d.Name == template && !d.Ed.Dirty);
            if (alreadyOpen != null)
            {
                await Activate(alreadyOpen);
                return;
            }

            EnsureTarget();
        }

        current = template;
        error = null;
        editing = null;
        if (runDoc == active) StopRun();

        try
        {
            var workspace = CwmReader.Read(await Http.GetByteArrayAsync(TemplateUrl(template)));
            await TryApplyTemplateStrings(workspace, template);
            adjust?.Invoke(workspace);
            ed.Load(workspace);
            await FitAsync();
            _ = AddRecent(template, "template");
        }
        catch (HttpRequestException)
        {
            // no connection and the template is not in the offline cache
            error = Loc.T("offline.templateMissing", template);
        }
        catch (Exception e)
        {
            error = e.Message;
        }
    }

    /// <summary>Replaces <c>$placeholders$</c> with the texts of the selected language. Templates without a text file are left as they are.</summary>
    private async Task TryApplyTemplateStrings(Workspace workspace, string template)
    {
        if (!settings.ReplacePlaceholders) return;
        try
        {
            var xml = await Http.GetByteArrayAsync(TemplateUrl(template[..^4] + ".xml")); // strip ".cwm"
            TemplateStrings.Apply(workspace, TemplateStrings.Load(xml, Loc.Lang));
        }
        catch (Exception)
        {
            // no .xml next to the template: nothing to replace
        }
    }

    /// <summary>Opens a <c>.cwm</c> (CrypTool 2) or <c>.json</c> (CrypWeb) workspace chosen by the user.</summary>
    private async Task OpenFile(InputFileChangeEventArgs e)
    {
        try
        {
            using var buffer = new MemoryStream();
            await e.File.OpenReadStream(MaxWorkspaceFileBytes).CopyToAsync(buffer);
            await OpenWorkspaceBytes(e.File.Name, buffer.ToArray());
        }
        catch (Exception ex)
        {
            error = "Could not open file: " + ex.Message;
        }
    }

    /// <summary>Opens the bytes of a workspace file (<c>.json</c> by name, everything else is read as <c>.cwm</c>) in a tab.</summary>
    private async Task OpenWorkspaceBytes(string fileName, byte[] bytes)
    {
        try
        {
            var isJson = fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
            var workspace = isJson
                ? WorkspaceEditor.FromJson(Encoding.UTF8.GetString(bytes))
                : CwmReader.Read(bytes);

            EnsureTarget();
            ed.Load(workspace);
            current = fileName;
            error = null;
            await FitAsync();
            _ = AddRecent(fileName, "file", bytes);
        }
        catch (Exception ex)
        {
            error = "Could not open file: " + ex.Message;
        }
    }

    /// <summary>
    ///     A file was dropped onto the page (canvas.js, ct2.initFileDrop) or the installed app was started with one
    ///     (x = y = -1): workspaces open in a tab, pictures are placed on the canvas where they were dropped.
    /// </summary>
    [JSInvokable]
    public async Task OnFileDrop(string fileName, string contentType, byte[] data, double clientX, double clientY)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is ".cwm" or ".json")
        {
            if (data.Length > MaxWorkspaceFileBytes) error = "Could not open file: it is larger than 50 MB.";
            else await OpenWorkspaceBytes(fileName, data);
        }
        else if (contentType.StartsWith("image/", StringComparison.Ordinal) && active.Kind == DocKind.Workspace)
        {
            if (data.Length > MaxImageFileBytes) error = "Could not add image: it is larger than 20 MB.";
            else await AddImageData(data, contentType, clientX >= 0 ? ToWorld(clientX, clientY) : null);
        }
        else
        {
            error = string.Format(Loc["drop.unsupported"], fileName);
        }

        StateHasChanged();
    }

    /// <summary>Adds an image file as a picture on the canvas (at most 400 units wide, in the middle of the view).</summary>
    private async Task AddImageFile(InputFileChangeEventArgs e)
    {
        try
        {
            using var buffer = new MemoryStream();
            await e.File.OpenReadStream(MaxImageFileBytes).CopyToAsync(buffer);
            await AddImageData(buffer.ToArray(), e.File.ContentType, null);
        }
        catch (Exception ex)
        {
            error = "Could not add image: " + ex.Message;
        }
    }

    /// <summary>Places a picture (at most 400 units wide) centred on <paramref name="at" />, or in the middle of the view.</summary>
    private async Task AddImageData(byte[] data, string contentType, (double x, double y)? at)
    {
        try
        {
            var dataUri = "data:" + contentType + ";base64," + Convert.ToBase64String(data);
            var naturalSize = await Js.InvokeAsync<double[]>("ct2.imageSize", dataUri);
            var width = Math.Min(naturalSize[0], 400);
            var height = naturalSize[0] > 0 ? naturalSize[1] * width / naturalSize[0] : 200;

            var (centerX, centerY) = at ?? Center();
            ed.Checkpoint();
            var image = ed.AddImage(data, centerX - width / 2, centerY - height / 2, width, height);
            SelectOnly(image.Id);
        }
        catch (Exception ex)
        {
            error = "Could not add image: " + ex.Message;
        }
    }

    /// <summary>A <c>data:</c> URI for the picture; the MIME type is guessed from the first byte (PNG, JPEG, GIF, BMP).</summary>
    private static string ImageUri(ImageNode image)
    {
        return ImageUris.GetValue(image.Data, MakeImageUri);
    }

    private static string MakeImageUri(byte[] data)
    {
        var mime = data.Length == 0
            ? "image/png"
            : data[0] switch
            {
                0x89 => "image/png",
                0xFF => "image/jpeg",
                0x47 => "image/gif",
                0x42 => "image/bmp",
                _ => "image/png"
            };
        return $"data:{mime};base64,{Convert.ToBase64String(data)}";
    }
}
