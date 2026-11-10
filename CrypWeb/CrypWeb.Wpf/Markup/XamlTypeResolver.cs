using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace System.Windows.Markup
{
    /// <summary>Finds the CLR type for an XML element name.</summary>
    internal static class XamlTypeResolver
    {
        public const string XamlLanguageNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

        /// <summary>CLR namespaces that the WPF presentation XML namespace stands for.</summary>
        private static readonly string[] PresentationNamespaces =
        {
            "System.Windows", "System.Windows.Controls", "System.Windows.Controls.Primitives", "System.Windows.Shapes",
            "System.Windows.Media", "System.Windows.Media.Animation", "System.Windows.Media.Imaging", "System.Windows.Media.Effects",
            "System.Windows.Media.Media3D", "System.Windows.Documents", "System.Windows.Data", "System.Windows.Input",
            "System.Windows.Markup",
        };

        private static readonly Dictionary<string, Type> LanguageTypes = new Dictionary<string, Type>
        {
            ["String"] = typeof(string), ["Double"] = typeof(double), ["Single"] = typeof(float), ["Int32"] = typeof(int),
            ["Int64"] = typeof(long), ["Boolean"] = typeof(bool), ["Char"] = typeof(char), ["Object"] = typeof(object),
            ["Static"] = typeof(StaticExtension), ["StaticExtension"] = typeof(StaticExtension),
            ["Null"] = typeof(NullExtension), ["NullExtension"] = typeof(NullExtension),
            ["Type"] = typeof(TypeExtension), ["TypeExtension"] = typeof(TypeExtension),
            ["Array"] = typeof(ArrayExtension), ["ArrayExtension"] = typeof(ArrayExtension),
        };

        private static readonly ConcurrentDictionary<string, Type> Cache = new ConcurrentDictionary<string, Type>();

        /// <summary>Type for an element or markup extension name in the given XML namespace; null if unknown.</summary>
        public static Type Resolve(string xmlNamespace, string name)
        {
            return Cache.GetOrAdd(xmlNamespace + "|" + name, _ => ResolveUncached(xmlNamespace ?? "", name));
        }

        private static Type ResolveUncached(string xmlNamespace, string name)
        {
            if (xmlNamespace == XamlLanguageNamespace)
            {
                return LanguageTypes.TryGetValue(name, out Type languageType) ? languageType : null;
            }

            if (xmlNamespace.StartsWith("clr-namespace:", StringComparison.Ordinal))
            {
                string clrNamespace = xmlNamespace.Substring("clr-namespace:".Length).Split(';')[0].Trim();
                return FindInLoadedAssemblies(clrNamespace + "." + name);
            }

            if (!xmlNamespace.Contains("/xaml/presentation"))
            {
                return null;
            }

            Assembly runtime = typeof(XamlTypeResolver).Assembly;
            foreach (string clrNamespace in PresentationNamespaces)
            {
                Type type = runtime.GetType(clrNamespace + "." + name, false);
                if (type != null)
                {
                    return type;
                }
            }

            // namespaces that other assemblies map into the presentation namespace (e.g. the Loc extension)
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (XmlnsDefinitionAttribute definition in SafeAttributes(assembly))
                {
                    Type type = assembly.GetType(definition.ClrNamespace + "." + name, false);
                    if (type != null)
                    {
                        return type;
                    }
                }
            }
            return null;
        }

        /// <summary>A type by its simple name in the presentation namespaces ("Canvas" in a property path).</summary>
        public static Type FindPresentationType(string simpleName)
        {
            return Resolve("http://schemas.microsoft.com/winfx/2006/xaml/presentation", simpleName);
        }

        private static Type FindInLoadedAssemblies(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }
            return Type.GetType(fullName, false);
        }

        private static IEnumerable<XmlnsDefinitionAttribute> SafeAttributes(Assembly assembly)
        {
            try
            {
                return assembly.GetCustomAttributes<XmlnsDefinitionAttribute>();
            }
            catch (Exception)
            {
                return Array.Empty<XmlnsDefinitionAttribute>();
            }
        }
    }

    /// <summary>Finds dependency properties by name ("Opacity", "Canvas.Left", "(UIElement.Opacity)").</summary>
    internal static class DependencyPropertyLookup
    {
        private const BindingFlags StaticFields = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;

        public static DependencyProperty Find(Type elementType, string name)
        {
            if (string.IsNullOrEmpty(name) || elementType == null)
            {
                return null;
            }

            name = name.Trim().Trim('(', ')');
            int dot = name.LastIndexOf('.');
            if (dot >= 0)
            {
                string owner = name.Substring(0, dot);
                owner = owner.Substring(owner.LastIndexOf(':') + 1);
                elementType = XamlTypeResolver.FindPresentationType(owner) ?? elementType;
                name = name.Substring(dot + 1);
            }
            return elementType.GetField(name + "Property", StaticFields)?.GetValue(null) as DependencyProperty;
        }

        /// <summary>
        /// Follows a storyboard property path such as <c>RenderTransform.(TransformGroup.Children)[0].(RotateTransform.Angle)</c>
        /// from <paramref name="root"/> to the object and dependency property it ends at.
        /// </summary>
        public static bool ResolvePath(DependencyObject root, string path, out DependencyObject owner, out DependencyProperty property)
        {
            owner = root;
            property = null;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            List<string> steps = SplitSteps(path);
            object current = root;
            for (int i = 0; i < steps.Count - 1 && current != null; i++)
            {
                current = Data.PropertyPathWalker.GetStep(current, steps[i], out bool found);
                if (!found)
                {
                    return false;
                }
            }

            owner = current as DependencyObject;
            if (owner == null)
            {
                return false;
            }
            property = Find(owner.GetType(), steps[steps.Count - 1]);
            return property != null;
        }

        /// <summary>Splits at the dots that are not inside parentheses.</summary>
        private static List<string> SplitSteps(string path)
        {
            var steps = new List<string>();
            int depth = 0;
            int start = 0;
            for (int i = 0; i < path.Length; i++)
            {
                char c = path[i];
                if (c == '(')
                {
                    depth++;
                }
                else if (c == ')')
                {
                    depth--;
                }
                else if (c == '.' && depth == 0)
                {
                    steps.Add(path.Substring(start, i - start));
                    start = i + 1;
                }
            }
            steps.Add(path.Substring(start));
            return steps;
        }
    }
}
