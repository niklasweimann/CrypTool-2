using System.Collections.Generic;
using System.Xml.Linq;

namespace System.Windows.Markup
{
    /// <summary>
    /// State of one XAML load: the root object, the declared names and the stack of objects whose resources are
    /// in scope. It is also the service provider handed to markup extensions.
    /// </summary>
    internal sealed class XamlContext : IServiceProvider, System.Xaml.IRootObjectProvider, IProvideValueTarget, IXamlTypeResolver
    {
        public XamlContext(object root)
        {
            RootObject = root;
        }

        public object RootObject { get; }

        /// <summary>Names declared with <c>x:Name</c>.</summary>
        public Dictionary<string, object> Names { get; private set; } = new Dictionary<string, object>();

        /// <summary>False inside templates: their named elements are not fields of the root object.</summary>
        public bool AssignsRootFields { get; private set; } = true;

        /// <summary>Elements, styles and dictionaries being built, innermost last; searched for resources.</summary>
        public List<object> Scopes { get; private set; } = new List<object>();

        /// <summary>The XML element being processed (resolves namespace prefixes in attribute values).</summary>
        public XElement CurrentElement { get; set; }

        public object TargetObject { get; set; }

        public object TargetProperty { get; set; }

        /// <summary>The XAML file being loaded ("PluginFolder/Relative/Path.xaml"); relative Source URIs start here.</summary>
        public string ResourcePath { get; set; }

        /// <summary>The assembly that embeds the XAML files of the plugin.</summary>
        public Reflection.Assembly Assembly { get; set; }

        public object GetService(Type serviceType) => serviceType != null && serviceType.IsInstanceOfType(this) ? this : null;

        /// <summary>A context for instantiating a template later: same resources in scope, its own names.</summary>
        public XamlContext ForkForTemplate()
        {
            return new XamlContext(RootObject)
            {
                Names = new Dictionary<string, object>(),
                AssignsRootFields = false,
                Scopes = new List<object>(Scopes),
                CurrentElement = CurrentElement,
                ResourcePath = ResourcePath,
                Assembly = Assembly,
            };
        }

        /// <summary>Resolves "prefix:TypeName" (or "TypeName") using the namespace declarations in scope.</summary>
        public Type Resolve(string qualifiedTypeName)
        {
            if (string.IsNullOrWhiteSpace(qualifiedTypeName))
            {
                return null;
            }
            qualifiedTypeName = qualifiedTypeName.Trim();
            int colon = qualifiedTypeName.IndexOf(':');
            string prefix = colon < 0 ? null : qualifiedTypeName.Substring(0, colon);
            string name = qualifiedTypeName.Substring(colon + 1);

            XNamespace xmlNamespace = prefix == null
                ? CurrentElement?.GetDefaultNamespace()
                : CurrentElement?.GetNamespaceOfPrefix(prefix);
            return XamlTypeResolver.Resolve(xmlNamespace?.NamespaceName ?? "http://schemas.microsoft.com/winfx/2006/xaml/presentation", name);
        }

        public bool TryFindResource(object key, out object value)
        {
            value = null;
            if (key == null)
            {
                return false;
            }
            for (int i = Scopes.Count - 1; i >= 0; i--)
            {
                ResourceDictionary resources = Scopes[i] switch
                {
                    FrameworkElement element => element.HasResources ? element.Resources : null,
                    Style style => style.Resources,
                    ResourceDictionary dictionary => dictionary,
                    _ => null,
                };
                if (resources != null && resources.TryGetResource(key, out value))
                {
                    return true;
                }
            }

            // resources of the elements above the root (when XAML is loaded into an attached element)
            return RootObject is FrameworkElement root && (value = root.TryFindResource(key)) != null;
        }

        /// <summary>Target type of the innermost style or control template being built (resolves <c>Setter.Property</c>).</summary>
        public Type CurrentStyleTargetType
        {
            get
            {
                for (int i = Scopes.Count - 1; i >= 0; i--)
                {
                    if (Scopes[i] is Style style)
                    {
                        return style.TargetType;
                    }
                    if (Scopes[i] is Controls.ControlTemplate template)
                    {
                        return template.TargetType;
                    }
                }
                return null;
            }
        }
    }
}
