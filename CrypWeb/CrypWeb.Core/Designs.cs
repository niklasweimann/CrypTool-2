namespace CrypTool.Web.Core;

/// <summary>
///     A design of the workspace: the colors of the categories and data types, the background of the canvas and the look of
///     the components. The user picks one of the ready-made designs (<see cref="Designs.Presets" />) or edits a custom one.
/// </summary>
public sealed class Design
{
    /// <summary>Look of the components: "classic" (as in CT2), "clean", "tinted" (clean with a tint of the category) or "contrast".</summary>
    public string Look { get; set; } = "tinted";

    /// <summary>Whether the interface (ribbon, lists, dialogs) is dark with this design; applies while the mode follows the design.</summary>
    public bool DarkUi { get; set; }

    /// <summary>The canvas is a vertical gradient from top to bottom (both #rrggbb).</summary>
    public string CanvasTop { get; set; } = "#c9ddf8";

    public string CanvasBottom { get; set; } = "#a9c8f2";

    /// <summary>Data type name (<see cref="Theme.DataTypeKeys" />) -> #rrggbb.</summary>
    public Dictionary<string, string> TypeColors { get; set; } = new();

    /// <summary>Category key (<see cref="Theme.CategoryKeys" />) -> #rrggbb.</summary>
    public Dictionary<string, string> CategoryColors { get; set; } = new();

    /// <summary>Whether the canvas is dark: labels and wire outlines then turn light.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsDark => (Luminance(CanvasTop) + Luminance(CanvasBottom)) / 2 < 0.4;

    private static double Luminance(string color)
    {
        var hex = color.TrimStart('#');
        if (hex.Length != 6 || !int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var rgb)) return 1;
        return (0.2126 * ((rgb >> 16) & 0xFF) + 0.7152 * ((rgb >> 8) & 0xFF) + 0.0722 * (rgb & 0xFF)) / 255;
    }

    public Design Clone()
    {
        return new Design
        {
            Look = Look,
            DarkUi = DarkUi,
            CanvasTop = CanvasTop,
            CanvasBottom = CanvasBottom,
            TypeColors = new Dictionary<string, string>(TypeColors),
            CategoryColors = new Dictionary<string, string>(CategoryColors)
        };
    }
}

/// <summary>A ready-made design. <see cref="Id" /> names its text ("design.&lt;id&gt;") in the language files.</summary>
public sealed record Preset(string Id, string Group, Design Design);

public static class Designs
{
    /// <summary>The id of the design the user edits.</summary>
    public const string Custom = "custom";

    public const string DefaultId = "default";

    public static readonly string[] Looks = { "classic", "clean", "tinted", "contrast" };

    // Order of the colors in the lists below: Theme.CategoryKeys (classic ciphers, symmetric, asymmetric, steganography,
    // hash functions, specific and generic cryptanalysis, protocols, tools, data input/output) and Theme.DataTypeKeys (Int32, Byte, Double,
    // Boolean, String, Object, BigInteger, Stream).
    private static Preset Make(string id, string group, string look, string top, string bottom, string categories, string types)
    {
        var design = new Design { Look = look, CanvasTop = top, CanvasBottom = bottom, DarkUi = group == "dark" || id == "contrast-dark" };
        var cat = categories.Split(' ');
        var typ = types.Split(' ');
        for (var i = 0; i < Theme.CategoryKeys.Length; i++) design.CategoryColors[Theme.CategoryKeys[i]] = cat[i];
        for (var i = 0; i < Theme.DataTypeKeys.Length; i++) design.TypeColors[Theme.DataTypeKeys[i]] = typ[i];
        return new Preset(id, group, design);
    }

    private const string Ct2Categories = "#b8c881 #d49090 #6789a2 #9acd32 #8abbbc #bc8aac #8c8abc #ff8c00 #8abc94 #8abc94";
    private const string Ct2Types = "#00ffff #228b22 #0000ff #800000 #808080 #9370db #00ced1 #ff8c00";
    private const string SoftTypes = "#4fc3d9 #4f9d57 #4a62d6 #a04a4a #8a8f98 #8f7fd0 #3fb3b0 #e8903a";

    /// <summary>The ready-made designs: light, dark and accessible ones.</summary>
    public static readonly IReadOnlyList<Preset> Presets = new[]
    {
        // ---- light ----
        Make("default", "light", "tinted", "#c9ddf8", "#a9c8f2", Ct2Categories, Ct2Types),
        Make("classic", "light", "classic", "#c9ddf8", "#a9c8f2", Ct2Categories, Ct2Types),
        Make("clean", "light", "clean", "#f3f5f9", "#e4e9f1",
            "#a9bd76 #cf8f8f #6f93b0 #8fbf3f #86b7b8 #b88fb0 #9594c2 #f0954a #86b596 #86b2b5", SoftTypes),
        Make("paper", "light", "tinted", "#fbf6ea", "#f0e6cf",
            "#a8b26a #c98a7a #7f9bb0 #8fb04a #86aaa6 #b58aa0 #9a92b8 #e09a4a #8fb08a #8ab0a0", SoftTypes),
        Make("ocean", "light", "tinted", "#d3ecf7", "#9fcbe6",
            "#6fb8c9 #7aa6d6 #4f86b8 #62c0a8 #5fb3c9 #8a9fd4 #6a8ec2 #3fa7d6 #56a8a0 #5675a8", "#2bb7d6 #3f9d7a #2f5fc8 #7a4a6a #6f8799 #7a73c8 #1f9fb0 #e0903a"),
        Make("forest", "light", "tinted", "#dcebd3", "#b3d2a6",
            "#a3c26a #c99a78 #7a9a8a #7fbf3f #8ab8a0 #b08a9a #8a9ab0 #e0a040 #6aa86f #6aa89c", "#4fbf9f #2f8f3f #3f6fc0 #8a4a3a #7f8a7f #8a7fc0 #3fa89a #e09a3a"),
        Make("sunset", "light", "tinted", "#fde6d3", "#f6c4a4",
            "#cdb36a #d98a7a #8a9ab8 #b5c24a #7fb3a8 #c98aa8 #a58ac2 #f08a3a #9ab87a #7ab887", "#4fb8c9 #4f9a4f #4a5ac8 #a04040 #8a8580 #9a7fc0 #3fb0a8 #f08a2a"),
        Make("lavender", "light", "tinted", "#e9e3f8", "#cfc4ee",
            "#b0bf86 #d29aa8 #7f93c0 #a0cf5a #92bfc8 #c490c8 #9a8ad0 #f09a5a #8fbf9f #8fbcbf", SoftTypes),
        Make("rose", "light", "tinted", "#fbe6ec", "#f2c6d4",
            "#bfc686 #d98a98 #8a9abf #a8cf6a #96bfc0 #c98ab0 #a89ac8 #f0905a #92bf9a #92bfba", SoftTypes),
        Make("slate", "light", "clean", "#e7eaef", "#cfd4dc",
            "#8a9a78 #a88a8a #6f8499 #8a9f5f #80999a #9a8498 #8c8aa8 #b89468 #7f9a8a #7f979a", "#6fa8b8 #5f8a5f #5a6fb0 #8a5a5a #8a8f98 #8a80b0 #5fa0a0 #b8884f"),
        // ---- dark ----
        Make("blueprint", "dark", "clean", "#274a82", "#16294d",
            "#b5d07a #e69a9a #7fb0e0 #a8e04a #8ad4d6 #d49ac8 #a8a6ea #ffa040 #8fd4a8 #8fced4", "#40e0f0 #50c060 #6a8aff #e06a6a #b0b6c0 #b49aff #40d0c8 #ffa030"),
        Make("midnight", "dark", "tinted", "#2c303c", "#1b1e27",
            "#a8c070 #d48a8a #70a0d0 #90d040 #70c0c0 #c080c0 #9090e0 #ff9030 #70c090 #70b6c0", "#30e0f0 #40c050 #5080ff #e05050 #a0a4ac #a080f0 #30d0c0 #ff9020"),
        Make("graphite", "dark", "clean", "#3c3c3c", "#242424",
            "#b0b890 #c09a9a #90a4b8 #a8c078 #90b0b0 #b898b0 #a0a0c0 #d0a070 #98b8a0 #98b8b7", "#70c8d8 #70b078 #7890d8 #c07878 #a8a8a8 #a898d0 #68b8b0 #d09850"),
        Make("solarized-dark", "dark", "clean", "#0a3a46", "#002b36",
            "#859900 #dc322f #268bd2 #b58900 #2aa198 #d33682 #6c71c4 #cb4b16 #93a1a1 #c4cccc", "#2aa198 #859900 #268bd2 #dc322f #93a1a1 #6c71c4 #2aa198 #cb4b16"),
        // ---- accessibility ----
        Make("contrast-light", "access", "contrast", "#ffffff", "#ffffff",
            "#1b5e20 #b71c1c #0d47a1 #827717 #006064 #4a148c #311b92 #e65100 #37474f #38374f", "#00b8d4 #1b5e20 #0000ff #b71c1c #555555 #6a1b9a #006064 #e65100"),
        Make("contrast-dark", "access", "contrast", "#000000", "#000000",
            "#ffd600 #ff5252 #40c4ff #76ff03 #18ffff #ea80fc #b388ff #ff9100 #e0e0e0 #b2b2b2", "#18ffff #76ff03 #448aff #ff5252 #e0e0e0 #ea80fc #64ffda #ff9100"),
        Make("colorblind-red-green", "access", "tinted", "#f5f5f5", "#e6e6e6",
            "#e69f00 #56b4e9 #009e73 #f0e442 #0072b2 #d55e00 #cc79a7 #999999 #333333 #616161", "#56b4e9 #009e73 #0072b2 #d55e00 #999999 #cc79a7 #f0e442 #e69f00"),
        Make("colorblind-blue-yellow", "access", "tinted", "#f7f3f3", "#e8e0e0",
            "#d81b60 #00897b #8e24aa #e53935 #455a64 #00acc1 #6d4c41 #ef6c00 #9e9e9e #cccccc", "#00acc1 #00897b #8e24aa #e53935 #9e9e9e #d81b60 #6d4c41 #ef6c00"),
        Make("monochrome", "access", "contrast", "#ffffff", "#e8e8e8",
            "#1a1a1a #4d4d4d #808080 #b3b3b3 #d0d0d0 #333333 #666666 #999999 #bfbfbf #919191", "#d9d9d9 #333333 #000000 #4d4d4d #808080 #b3b3b3 #bfbfbf #666666"),
        Make("solarized-light", "light", "clean", "#fdf6e3", "#eee8d5",
            "#859900 #dc322f #268bd2 #b58900 #2aa198 #d33682 #6c71c4 #cb4b16 #93a1a1 #c4cccc", "#2aa198 #859900 #268bd2 #dc322f #93a1a1 #6c71c4 #2aa198 #cb4b16"),
    };

    public static Preset? Find(string id)
    {
        foreach (var preset in Presets)
            if (preset.Id == id) return preset;
        return null;
    }

    public static Preset Default => Presets[0];
}
