using System.Text.Json;
using CrypTool.Web.Core;

namespace CrypWeb.Tests;

/// <summary>
/// Coverage of the CrypTool function list (https://www.cryptool.org/de/functions/): checks that every component and
/// template that reference/functions.json names for an entry really exists, and prints how many entries are done.
/// The file is written by tools-gen-functions.py.
/// </summary>
public static class FunctionsCommand
{
    /// <summary>Returns false (and prints what is wrong) when an entry refers to a component or template that does not exist.</summary>
    public static bool Run(string templateRoot, string[] args)
    {
        var path = args.Length > 2 ? args[2] : Path.Combine(templateRoot, "..", "CrypWeb", "reference", "functions.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        int done = 0, partial = 0, open = 0, broken = 0;
        var openBySource = new SortedDictionary<string, int>();

        foreach (var entry in document.RootElement.EnumerateArray())
        {
            var name = entry.GetProperty("name").GetString();
            var status = entry.GetProperty("status").GetString();
            if (status == "open")
            {
                open++;
                var source = string.Join("+", new[] { "ct1", "jct", "cto" }.Where(k => entry.GetProperty(k).GetBoolean()).Select(k => k.ToUpperInvariant()));
                openBySource[source == "" ? "none" : source] = openBySource.GetValueOrDefault(source == "" ? "none" : source) + 1;
                continue;
            }
            if (status == "partial") partial++; else done++;

            foreach (var component in entry.GetProperty("components").EnumerateObject())
            {
                if (PluginRegistry.FindType(component.Value.GetString()!) == null)
                {
                    Console.WriteLine($"{name}: component '{component.Name}' ({component.Value}) is not in the plugin registry");
                    broken++;
                }
            }
            foreach (var template in entry.GetProperty("templates").EnumerateObject())
            {
                if (!File.Exists(Path.Combine(templateRoot, template.Value.GetString()!)))
                {
                    Console.WriteLine($"{name}: template '{template.Name}' ({template.Value}) does not exist");
                    broken++;
                }
            }
        }

        Console.WriteLine($"function list: {done} done, {partial} partial, {open} open ({string.Join(", ", openBySource.Select(p => $"{p.Key} {p.Value}"))}); {broken} references broken");
        return broken == 0;
    }
}
