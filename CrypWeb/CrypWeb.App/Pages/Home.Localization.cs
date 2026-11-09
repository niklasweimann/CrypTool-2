using Microsoft.AspNetCore.Components;

namespace CrypWeb.App.Pages;

// Localization glue for the workspace page (kept in its own file so the main page code stays untouched).
public partial class Home
{
    [Inject] Localizer Loc { get; set; } = default!;

    protected override void OnInitialized() { Loc.Changed += OnLanguageChanged; }

    void OnLanguageChanged() => _ = InvokeAsync(async () =>
    {
        // Template texts ($placeholders$) are resolved when a template is loaded: reload an unmodified template in the new language.
        if (current != null && !ed.Dirty && templates.Contains(current))
        {
            double px = panX, py = panY, z = zoom;
            await OpenTemplate(current);
            panX = px; panY = py; zoom = z;
        }
        StateHasChanged();
    });

    int ribbonTab; // 0 = Home, 1 = View

    async Task SetLanguage(string code) => await Loc.SetLanguageAsync(code);

    void DetachLocalizer() => Loc.Changed -= OnLanguageChanged;
}
