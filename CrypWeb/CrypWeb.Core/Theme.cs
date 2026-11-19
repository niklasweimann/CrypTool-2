namespace CrypTool.Web.Core;

/// <summary>Colors taken from the original CT2 workspace (WorkspaceManagerModel/Properties/Settings.settings).</summary>
public static class Theme
{
    /// <summary>User overrides from the global settings (category key -> #rrggbb).</summary>
    public static Dictionary<string, string> CategoryOverrides { get; } = new();

    /// <summary>User overrides from the global settings (data type name -> #rrggbb).</summary>
    public static Dictionary<string, string> TypeOverrides { get; } = new();

    public static readonly string[] DataTypeKeys =
    {
        "System.Int32", "System.Byte", "System.Double", "System.Boolean", "System.String",
        "System.Object", "System.Numerics.BigInteger", "Stream",
    };

    public static readonly string[] CategoryKeys =
    {
        "CiphersClassic", "CiphersModernSymmetric", "CiphersModernAsymmetric", "Steganography", "HashFunctions",
        "CryptanalysisSpecific", "CryptanalysisGeneric", "Protocols", "ToolsMisc",
    };

    public static string CategoryColor(string category)
    {
        // all "Tools..." categories share one color setting
        var key = category.StartsWith("Tools") ? "ToolsMisc" : category;
        return CategoryOverrides.TryGetValue(key, out var color) ? color : DefaultCategoryColor(category);
    }

    /// <summary>
    /// The background of a component window in CT2: the category color made lighter as
    /// <c>ControlPaint.Light(ControlPaint.LightLight(color))</c> does (WorkspaceManagerModel ColorHelper.GetColorLight).
    /// In HLS terms the lightness moves 62.5 % of the way to white; hue and saturation stay. Transparent
    /// presentations show this color behind them.
    /// </summary>
    /// <summary>The color moved by <paramref name="amount" /> (0..1) towards white; "#rrggbb" in and out.</summary>
    public static string Tint(string color, double amount)
    {
        string hex = color.TrimStart('#');
        if (hex.Length != 6 || !int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int rgb))
        {
            return "#ffffff";
        }
        int Mix(int channel) => (int)Math.Round(channel + (255 - channel) * amount);
        return $"#{Mix((rgb >> 16) & 0xFF):x2}{Mix((rgb >> 8) & 0xFF):x2}{Mix(rgb & 0xFF):x2}";
    }

    public static string CategoryLightColor(string category)
    {
        string hex = CategoryColor(category).TrimStart('#');
        if (hex.Length != 6 || !int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int rgb))
        {
            return "#ffffff";
        }
        double r = ((rgb >> 16) & 0xFF) / 255.0, g = ((rgb >> 8) & 0xFF) / 255.0, b = (rgb & 0xFF) / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        double lightness = (max + min) / 2;
        double saturation = max == min ? 0 : lightness < 0.5 ? (max - min) / (max + min) : (max - min) / (2 - max - min);
        double hue = 0;
        if (max != min)
        {
            double d = max - min;
            hue = max == r ? (g - b) / d + (g < b ? 6 : 0) : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
            hue /= 6;
        }

        // LightLight moves the lightness halfway to white, Light then a quarter of the rest
        lightness += (1 - lightness) * 0.625;

        double q = lightness < 0.5 ? lightness * (1 + saturation) : lightness + saturation - lightness * saturation;
        double p0 = 2 * lightness - q;
        int Channel(double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            double v = t < 1.0 / 6 ? p0 + (q - p0) * 6 * t : t < 0.5 ? q : t < 2.0 / 3 ? p0 + (q - p0) * (2.0 / 3 - t) * 6 : p0;
            return (int)Math.Round(Math.Clamp(v, 0, 1) * 255);
        }
        return saturation == 0
            ? $"#{(int)Math.Round(lightness * 255):x2}{(int)Math.Round(lightness * 255):x2}{(int)Math.Round(lightness * 255):x2}"
            : $"#{Channel(hue + 1.0 / 3):x2}{Channel(hue):x2}{Channel(hue - 1.0 / 3):x2}";
    }

    public static string TypeColor(string dataType)
    {
        var type = dataType.Replace("[]", "");
        if (type.EndsWith("Stream")) type = "Stream";
        return TypeOverrides.TryGetValue(type, out var color) ? color : DefaultTypeColor(dataType);
    }

    public static string DefaultCategoryColor(string category) => category switch
    {
        "CiphersClassic" => "#b8c881",
        "CiphersModernSymmetric" => "#d49090",
        "CiphersModernAsymmetric" => "#6789a2",
        "Steganography" => "#9acd32",
        "HashFunctions" => "#8abbbc",
        "CryptanalysisSpecific" => "#bc8aac",
        "CryptanalysisGeneric" => "#8c8abc",
        "Protocols" => "#ff8c00",
        "ToolsBoolean" or "ToolsDataflow" or "ToolsDataInputOutput" or "ToolsRandomNumbers" or "ToolsCodes" or "ToolsMisc" => "#8abc94",
        _ => "#a3d090",
    };

    public static string DefaultTypeColor(string dataType)
    {
        var type = dataType.Replace("[]", "");
        return type switch
        {
            "System.Int32" => "#00ffff",
            "System.Byte" => "#228b22",
            "System.Double" => "#0000ff",
            "System.Boolean" => "#800000",
            "System.String" => "#808080",
            "System.Object" => "#9370db",
            "System.Numerics.BigInteger" => "#00ced1",
            _ when type.EndsWith("ICrypToolStream") || type.EndsWith("Stream") => "#ff8c00",
            _ => "#a3d090",
        };
    }
}
