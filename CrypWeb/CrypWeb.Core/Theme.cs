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
