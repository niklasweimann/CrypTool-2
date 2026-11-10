using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Linq;

namespace System.Windows.Markup
{
    /// <summary>
    /// Loads XAML at run time. The generated <c>InitializeComponent()</c> of a presentation calls
    /// <see cref="LoadComponent"/>, which builds the element tree into the presentation object, assigns its named
    /// fields and connects the event handlers declared in the XAML.
    /// </summary>
    public static class XamlLoader
    {
        /// <summary>Prefix of the embedded resource names under which the build stores the XAML files.</summary>
        public const string ResourcePrefix = "xaml/";

        /// <summary>
        /// Loads the XAML embedded as <c>xaml/{resourcePath}</c> in the assembly of <paramref name="root"/>.
        /// <paramref name="resourcePath"/> is "PluginFolder/Relative/Path.xaml".
        /// </summary>
        public static void LoadComponent(object root, string resourcePath)
        {
            Assembly assembly = root.GetType().Assembly;
            using Stream stream = assembly.GetManifestResourceStream(ResourcePrefix + resourcePath);
            if (stream == null)
            {
                throw new InvalidOperationException("XAML resource '" + ResourcePrefix + resourcePath + "' not found in " + assembly.GetName().Name + ".");
            }
            using var reader = new StreamReader(stream);
            Load(root, reader.ReadToEnd(), resourcePath);
        }

        /// <summary>Builds the content described by <paramref name="xaml"/> into the existing object <paramref name="root"/>.</summary>
        public static void Load(object root, string xaml, string resourcePath = null)
        {
            XElement element = XDocument.Parse(xaml, LoadOptions.PreserveWhitespace).Root;
            var context = new XamlContext(root);

            if (root is FrameworkElement frameworkElement)
            {
                frameworkElement.NameScope = context.Names;
                if (!string.IsNullOrEmpty(resourcePath))
                {
                    int firstSlash = resourcePath.IndexOf('/');
                    int lastSlash = resourcePath.LastIndexOf('/');
                    frameworkElement.ResourceRoot = firstSlash < 0 ? "" : resourcePath.Substring(0, firstSlash);
                    frameworkElement.ResourceDirectory = lastSlash > firstSlash ? resourcePath.Substring(firstSlash + 1, lastSlash - firstSlash) : "";
                }
            }

            new XamlObjectBuilder(context).Populate(root, element);
        }

        /// <summary>Creates the object described by the root element of <paramref name="xaml"/>.</summary>
        public static object Parse(string xaml)
        {
            XElement element = XDocument.Parse(xaml, LoadOptions.PreserveWhitespace).Root;
            Type type = XamlTypeResolver.Resolve(element.Name.NamespaceName, element.Name.LocalName);
            if (type == null)
            {
                throw new InvalidOperationException("Unknown XAML root element '" + element.Name.LocalName + "'.");
            }
            object root = Activator.CreateInstance(type);
            Load(root, xaml);
            return root;
        }
    }

    /// <summary>WPF's own reader API, as used by plugin code.</summary>
    public static class XamlReader
    {
        public static object Parse(string xamlText) => XamlLoader.Parse(xamlText);

        public static object Load(Stream stream)
        {
            using var reader = new StreamReader(stream);
            return XamlLoader.Parse(reader.ReadToEnd());
        }

        public static object Load(System.Xml.XmlReader reader) => XamlLoader.Parse(XDocument.Load(reader).ToString());
    }
}

namespace System.Windows
{
    public static class ApplicationResources
    {
        /// <summary>Component loading by URI is replaced by the generated InitializeComponent; kept for source compatibility.</summary>
        public static void LoadComponent(object component, Uri resourceLocator) { }
    }
}
