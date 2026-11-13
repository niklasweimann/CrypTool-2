using System.Reflection;
using CrypTool.PluginBase;

namespace CrypTool.Web.Core;

/// <summary>Finds the plugin classes (<see cref="ICrypComponent"/>) in the loaded assemblies and creates components by type name.</summary>
public static class PluginRegistry
{
    static Dictionary<string, Type>? map;

    /// <summary>Forget the scan (the next lookup scans again).</summary>
    public static void Reset() => map = null;

    static Dictionary<string, Type> Map
    {
        get
        {
            if (map == null) Build();
            return map!;
        }
    }

    static void TryLoad(string assemblyName)
    {
        try
        {
            Assembly.Load(assemblyName);
        }
        catch (Exception) { /* optional assembly (not part of this build) */ }
    }

    static void Build()
    {
        map = new(StringComparer.OrdinalIgnoreCase);
        TryLoad("CrypWeb.Plugins");
        foreach (var name in IsolatedAssemblies.Names) TryLoad(name);

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null).ToArray()!;
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var type in types)
            {
                bool isPlugin = type.FullName != null && !type.IsAbstract
                    && typeof(ICrypComponent).IsAssignableFrom(type)
                    && type.GetConstructor(Type.EmptyTypes) != null;
                if (isPlugin) map[type.FullName!] = type;
            }
        }
    }

    public static Type? FindType(string typeName) => Map.GetValueOrDefault(typeName);

    public static bool IsSupported(string typeName) =>
        NativeComponent.TryCreate(typeName) != null || Map.ContainsKey(typeName);

    public static RunComponent? Create(string typeName)
    {
        // the browser-native input/output components win over the plugins of the same name: the app draws them
        // as its own text fields (the plugins are compiled too, but their presentation would not be shown)
        if (NativeComponent.TryCreate(typeName) is { } native) return native;
        if (Map.TryGetValue(typeName, out var type)) return new PluginComponent((ICrypComponent)Activator.CreateInstance(type)!);
        return null;
    }
}
