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
    private async Task OpenTemplate(string template, bool inPlace = false)
    {
        if (!inPlace)
        {
            var alreadyOpen = docs.FirstOrDefault(d => d.Kind == DocKind.Workspace && d.Name == template && !d.Ed.Dirty);
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
            ed.Load(workspace);
            await FitAsync();
            _ = AddRecent(template, "template");
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
            var bytes = buffer.ToArray();
            var isJson = e.File.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
            var workspace = isJson
                ? WorkspaceEditor.FromJson(Encoding.UTF8.GetString(bytes))
                : CwmReader.Read(bytes);

            EnsureTarget();
            ed.Load(workspace);
            current = e.File.Name;
            error = null;
            await FitAsync();
            _ = AddRecent(e.File.Name, "file", bytes);
        }
        catch (Exception ex)
        {
            error = "Could not open file: " + ex.Message;
        }
    }

    /// <summary>Adds an image file as a picture on the canvas (at most 400 units wide, in the middle of the view).</summary>
    private async Task AddImageFile(InputFileChangeEventArgs e)
    {
        try
        {
            using var buffer = new MemoryStream();
            await e.File.OpenReadStream(MaxImageFileBytes).CopyToAsync(buffer);
            var data = buffer.ToArray();

            var dataUri = "data:" + e.File.ContentType + ";base64," + Convert.ToBase64String(data);
            var naturalSize = await Js.InvokeAsync<double[]>("ct2.imageSize", dataUri);
            var width = Math.Min(naturalSize[0], 400);
            var height = naturalSize[0] > 0 ? naturalSize[1] * width / naturalSize[0] : 200;

            var (centerX, centerY) = Center();
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
        var data = image.Data;
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
