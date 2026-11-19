using CrypTool.Web.Core;
using Microsoft.AspNetCore.Components.Routing;

namespace CrypWeb.App.Pages;

// The documentation tab (Components/DocsPage.razor). The address bar names the page ("?doc=component/<type>"), so that
// links inside the articles, the ribbon, F1 and shared links all work the same way: the address changes and this
// partial shows the tab with that page.
public partial class Home
{
    /// <summary>The page the documentation tab shows ("component/&lt;type&gt;", ...); empty for the overview.</summary>
    private string docsTarget = "";

    private void AttachDocs()
    {
        Nav.LocationChanged += OnDocsLocationChanged;
    }

    private void DetachDocs()
    {
        Nav.LocationChanged -= OnDocsLocationChanged;
    }

    private void OnDocsLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        if (DocLink.Parse(e.Location) is not { } target) return;
        _ = InvokeAsync(() =>
        {
            ShowDocs(target);
            StateHasChanged();
        });
    }

    /// <summary>Shows the documentation tab with the page; null keeps the page it showed before.</summary>
    private void ShowDocs(string? target = null)
    {
        if (target != null) docsTarget = target;
        ShowSingleton(DocKind.Docs);
    }

    /// <summary>Opens the documentation of a component (the Help item of its menu, F1).</summary>
    private void ShowHelp(PluginNode p)
    {
        Nav.NavigateTo(DocLink.Href("component", p.TypeName));
    }

    /// <summary>F1: the documentation of the selected component, else the documentation.</summary>
    private void HelpKey()
    {
        if (active.Kind == DocKind.Workspace && SelectedPlugin is { } plugin) ShowHelp(plugin);
        else ShowDocs();
    }

    /// <summary>A link of the address bar (shared or reloaded) opens the tab at start-up.</summary>
    private void OpenDocsFromAddress()
    {
        if (DocLink.Parse(Nav.Uri) is { } target) ShowDocs(target);
    }
}
