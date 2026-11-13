namespace CrypTool.Web.Core;

/// <summary>Which output may be connected to which input.</summary>
public static class ConnectionRules
{
    const string BigInteger = "System.Numerics.BigInteger";
    const string String = "System.String";
    const string Bytes = "System.Byte[]";
    const string Stream = "CrypTool.PluginBase.IO.ICrypToolStream";

    static readonly Dictionary<string, Type?> typeCache = new();

    static Type? Resolve(string name)
    {
        if (string.IsNullOrEmpty(name)) return typeof(object);
        if (typeCache.TryGetValue(name, out var type)) return type;
        type = Type.GetType(name) ?? Type.GetType(name + ", CrypPluginBase") ?? Type.GetType(name + ", System.Runtime.Numerics");
        return typeCache[name] = type;
    }

    static bool IsInteger(string type) => type is "System.Int32" or "System.Int64";

    /// <summary>
    /// Same rules as the original WorkspaceModel.CheckConnection: green (the type is assignable)
    /// or yellow (the plugin base converts the value).
    /// </summary>
    public static bool Compatible(string from, string to)
    {
        if (from == to || from == "System.Object" || to == "System.Object" || from == "" || to == "") return true;

        var fromType = Resolve(from);
        var toType = Resolve(to);
        if (fromType != null && toType != null
            && (fromType == toType || fromType.IsSubclassOf(toType) || fromType.GetInterfaces().Contains(toType)))
            return true;

        // conversions done by the plugin base ("yellow" connections)
        return (IsInteger(from) && to == BigInteger) || (IsInteger(to) && from == BigInteger)
            || (to == String && from == Bytes) || (to == Bytes && from == String)
            || (to == String && (IsInteger(from) || from == BigInteger || from == "System.Boolean" || from == Stream))
            || (to == Stream && from == String);
    }
}
