namespace CrypWeb.App.Pages;

/// <summary>An entry of the start center's "recent" list (kept in localStorage).</summary>
/// <param name="Name">A template path, or a file name.</param>
/// <param name="Kind">"template" or "file".</param>
/// <param name="Data">For files: the content as base64 (the browser cannot reopen a file by path); null if it was too large to keep.</param>
public sealed record Recent(string Name, string Kind, string? Data);
