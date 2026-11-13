using System.Net.Http.Json;
using Microsoft.JSInterop;

namespace CrypWeb.App;

/// <summary>
///     Tiny key → string lookup for the UI (wwwroot/i18n/&lt;lang&gt;.json, English is the fallback).
///     The choice is stored in localStorage and defaults to the browser language.
///     Add a language by dropping a new JSON file next to en.json and listing it in <see cref="Languages" />.
/// </summary>
public sealed class Localizer
{
    private const string StorageKey = "ct2.lang";
    public static readonly (string Code, string Name)[] Languages = { ("en", "English"), ("de", "Deutsch") };
    private Dictionary<string, string> fallback = new(), current = new();

    public Localizer(HttpClient http, IJSRuntime js)
    {
        Http = http;
        Js = js;
    }

    private HttpClient Http { get; }
    private IJSRuntime Js { get; }

    public string Lang { get; private set; } = "en";

    public string this[string key] => current.TryGetValue(key, out var v) ? v : fallback.TryGetValue(key, out v) ? v : key;
    public event Action? Changed;

    public string T(string key, params object[] args)
    {
        var s = this[key];
        try
        {
            return args.Length == 0 ? s : string.Format(s, args);
        }
        catch (FormatException)
        {
            return s;
        }
    }

    public async Task InitAsync()
    {
        fallback = await Load("en");
        string? code = null;
        try
        {
            code = await Js.InvokeAsync<string?>("eval",
                $"(() => {{ try {{ return localStorage.getItem('{StorageKey}'); }} catch (e) {{ return null; }} }})()");
        }
        catch (Exception)
        {
            /* no stored language: fall back to the browser language */
        }

        if (string.IsNullOrEmpty(code))
            try
            {
                code = (await Js.InvokeAsync<string?>("eval", "navigator.language || 'en'"))?.Split('-')[0].ToLowerInvariant();
            }
            catch (Exception)
            {
                /* no browser language: English */
            }

        await Apply(Languages.Any(l => l.Code == code) ? code! : "en", false, false);
    }

    public Task SetLanguageAsync(string code)
    {
        return Apply(code, true, true);
    }

    private async Task Apply(string code, bool persist, bool notify)
    {
        current = code == "en" ? fallback : await Load(code);
        Lang = code;
        // The engine worker reads this when it starts a run (plugin log messages use the same culture).
        try
        {
            await Js.InvokeVoidAsync("eval", $"window.ct2lang = '{code}'; document.documentElement.lang = '{code}';");
        }
        catch (Exception)
        {
            /* the page language attribute is cosmetic */
        }

        if (persist)
            try
            {
                await Js.InvokeVoidAsync("eval", $"try {{ localStorage.setItem('{StorageKey}', '{code}'); }} catch (e) {{ }}");
            }
            catch (Exception)
            {
                /* storage unavailable (private window): the choice is not remembered */
            }

        if (notify) Changed?.Invoke();
    }

    private async Task<Dictionary<string, string>> Load(string code)
    {
        try
        {
            return await Http.GetFromJsonAsync<Dictionary<string, string>>($"i18n/{code}.json") ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }
}
