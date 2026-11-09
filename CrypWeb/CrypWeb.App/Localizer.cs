using System.Net.Http.Json;
using Microsoft.JSInterop;

namespace CrypWeb.App;

/// <summary>
/// Tiny key → string lookup for the UI (wwwroot/i18n/&lt;lang&gt;.json, English is the fallback).
/// The choice is stored in localStorage and defaults to the browser language.
/// Add a language by dropping a new JSON file next to en.json and listing it in <see cref="Languages"/>.
/// </summary>
public sealed class Localizer
{
    public static readonly (string Code, string Name)[] Languages = { ("en", "English"), ("de", "Deutsch") };
    const string StorageKey = "ct2.lang";

    readonly HttpClient http;
    readonly IJSRuntime js;
    Dictionary<string, string> fallback = new(), current = new();

    public Localizer(HttpClient http, IJSRuntime js) { this.http = http; this.js = js; }

    public string Lang { get; private set; } = "en";
    public event Action? Changed;

    public string this[string key] => current.TryGetValue(key, out var v) ? v : fallback.TryGetValue(key, out v) ? v : key;
    public string T(string key, params object[] args) { var s = this[key]; try { return args.Length == 0 ? s : string.Format(s, args); } catch (FormatException) { return s; } }

    public async Task InitAsync()
    {
        fallback = await Load("en");
        string? code = null;
        try { code = await js.InvokeAsync<string?>("eval", $"(() => {{ try {{ return localStorage.getItem('{StorageKey}'); }} catch (e) {{ return null; }} }})()"); } catch { }
        if (string.IsNullOrEmpty(code))
        {
            try { code = (await js.InvokeAsync<string?>("eval", "navigator.language || 'en'"))?.Split('-')[0].ToLowerInvariant(); } catch { }
        }
        await Apply(Languages.Any(l => l.Code == code) ? code! : "en", persist: false, notify: false);
    }

    public Task SetLanguageAsync(string code) => Apply(code, persist: true, notify: true);

    async Task Apply(string code, bool persist, bool notify)
    {
        current = code == "en" ? fallback : await Load(code);
        Lang = code;
        // The engine worker reads this when it starts a run (plugin log messages use the same culture).
        try { await js.InvokeVoidAsync("eval", $"window.ct2lang = '{code}'; document.documentElement.lang = '{code}';"); } catch { }
        if (persist) { try { await js.InvokeVoidAsync("eval", $"try {{ localStorage.setItem('{StorageKey}', '{code}'); }} catch (e) {{ }}"); } catch { } }
        if (notify) Changed?.Invoke();
    }

    async Task<Dictionary<string, string>> Load(string code)
    {
        try { return await http.GetFromJsonAsync<Dictionary<string, string>>($"i18n/{code}.json") ?? new(); }
        catch { return new(); }
    }
}
